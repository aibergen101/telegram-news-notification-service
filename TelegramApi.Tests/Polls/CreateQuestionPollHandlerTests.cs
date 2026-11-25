using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Polls;

public sealed class CreateQuestionPollHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ITelegramService _telegram = Substitute.For<ITelegramService>();

    public void Dispose() => _database.Dispose();

    private static CreateQuestionPollCommand Command() =>
        new("test_channel", "Lunch?", [new PollOptionDto("Pizza"), new PollOptionDto("Sushi")], true, false, 60);

    private async Task<PollDetails> Handle(CreateQuestionPollCommand command)
    {
        await using var db = _database.CreateContext();
        return await CreateQuestionPollHandler.Handle(
            command, db, _telegram, NullLogger<CreateQuestionPollHandler>.Instance, CancellationToken.None);
    }

    [Fact]
    public async Task Sends_poll_and_saves_sent_record()
    {
        _telegram.SendPollAsync(default!, default!, default!, default, default, default, default)
            .ReturnsForAnyArgs(new SentMessage(7, TestData.ChatId));

        var result = await Handle(Command());

        await _telegram.Received(1).EnsureCanPublishAsync(TestData.Alias, Arg.Any<CancellationToken>());
        await _telegram.Received(1).SendPollAsync(
            TestData.Alias,
            "Lunch?",
            Arg.Is<IReadOnlyList<string>>(o => o.SequenceEqual(new[] { "Pizza", "Sushi" })),
            true,
            false,
            60,
            Arg.Any<CancellationToken>());

        Assert.Equal(PublicationStatus.Sent, result.Status);
        Assert.Equal(7, result.TgMessageId);
        Assert.Equal(TestData.ChatId, result.TgChatId);
        Assert.True(result.AllowMultipleAnswers);
        Assert.False(result.IsAnonymous);
        Assert.Equal(60, result.Duration);
        Assert.False(result.IsClosed);
        Assert.Equal(["Pizza", "Sushi"], result.Options.Select(o => o.Text));

        await using var check = _database.CreateContext();
        var saved = await check.Polls.Include(p => p.Options).SingleAsync();
        Assert.Equal(PublicationStatus.Sent, saved.Status);
        Assert.Equal(2, saved.Options.Count);
    }

    [Fact]
    public async Task Saves_failed_poll_with_telegram_error()
    {
        _telegram.SendPollAsync(default!, default!, default!, default, default, default, default)
            .ThrowsAsyncForAnyArgs(new TelegramOperationException("Bad Request: poll can't be sent", 400));

        var thrown = await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        var saved = await check.Polls.SingleAsync();
        Assert.Equal(saved.Id, thrown.RecordId);
        Assert.Equal(PublicationStatus.Failed, saved.Status);
        Assert.Equal(400, saved.ErrorCode);
        Assert.NotNull(saved.ErrorDate);
    }

    [Fact]
    public async Task Does_not_create_record_when_channel_is_unavailable()
    {
        _telegram.EnsureCanPublishAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ChannelNotFoundException("not found"));

        await Assert.ThrowsAsync<ChannelNotFoundException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        Assert.Empty(check.Polls);
    }
}
