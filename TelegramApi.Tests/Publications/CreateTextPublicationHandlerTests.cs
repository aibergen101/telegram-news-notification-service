using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Publications;

public sealed class CreateTextPublicationHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ITelegramService _telegram = Substitute.For<ITelegramService>();

    public void Dispose() => _database.Dispose();

    private async Task<PublicationDetails> Handle(CreateTextPublicationCommand command)
    {
        await using var db = _database.CreateContext();
        return await CreateTextPublicationHandler.Handle(
            command, db, _telegram, NullLogger<CreateTextPublicationHandler>.Instance, CancellationToken.None);
    }

    private static CreateTextPublicationCommand Command(string alias = "test_channel") =>
        new(alias, "Hello", TextFormat.Html, [new ButtonDto("First", "https://a.example"), new ButtonDto("Second", "https://b.example")]);

    [Fact]
    public async Task Sends_text_and_saves_sent_publication()
    {
        _telegram.SendTextAsync(default!, default!, default, default!, default)
            .ReturnsForAnyArgs(new SentMessage(42, TestData.ChatId));

        var result = await Handle(Command());

        await _telegram.Received(1).EnsureCanPublishAsync(TestData.Alias, Arg.Any<CancellationToken>());
        await _telegram.Received(1).SendTextAsync(
            TestData.Alias,
            "Hello",
            TextFormat.Html,
            Arg.Is<IReadOnlyList<ButtonDto>>(b => b.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Equal(PublicationStatus.Sent, result.Status);
        Assert.Equal(TestData.Alias, result.ChannelAlias);
        Assert.Equal(42, result.TgMessageId);
        Assert.Equal(TestData.ChatId, result.TgChatId);
        Assert.NotNull(result.SentAt);
        Assert.Equal(["First", "Second"], result.Buttons.Select(b => b.Text));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.Include(p => p.Buttons).SingleAsync();
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(PublicationStatus.Sent, saved.Status);
        Assert.Equal(42, saved.TgMsgId);
        Assert.Equal([0, 1], saved.Buttons.OrderBy(b => b.Order).Select(b => b.Order));
    }

    [Fact]
    public async Task Does_not_create_record_when_channel_is_unavailable()
    {
        _telegram.EnsureCanPublishAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new ChannelNotFoundException("not found"));

        await Assert.ThrowsAsync<ChannelNotFoundException>(() => Handle(Command()));

        await _telegram.DidNotReceiveWithAnyArgs().SendTextAsync(default!, default!, default, default!, default);
        await using var check = _database.CreateContext();
        Assert.Empty(check.Publications);
    }

    [Fact]
    public async Task Saves_failed_publication_with_telegram_error()
    {
        var error = new TelegramOperationException("Bad Request: can't parse entities", errorCode: 400);
        _telegram.SendTextAsync(default!, default!, default, default!, default).ThrowsAsyncForAnyArgs(error);

        var thrown = await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.SingleAsync();
        Assert.Equal(saved.Id, thrown.RecordId);
        Assert.Equal(PublicationStatus.Failed, saved.Status);
        Assert.Equal(400, saved.ErrorCode);
        Assert.Equal("Bad Request: can't parse entities", saved.ErrorText);
        Assert.NotNull(saved.ErrorDate);
        Assert.Null(saved.SentAt);
    }

    [Fact]
    public async Task Saves_failed_publication_on_unexpected_error()
    {
        _telegram.SendTextAsync(default!, default!, default, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.SingleAsync();
        Assert.Equal(PublicationStatus.Failed, saved.Status);
        Assert.Null(saved.ErrorCode);
        Assert.Equal("Unexpected error while publishing.", saved.ErrorText);
    }

    [Fact]
    public async Task Saves_failed_publication_when_cancelled()
    {
        _telegram.SendTextAsync(default!, default!, default, default!, default)
            .ThrowsAsyncForAnyArgs(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.SingleAsync();
        Assert.Equal(PublicationStatus.Failed, saved.Status);
        Assert.Contains("cancelled", saved.ErrorText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Truncates_long_error_text()
    {
        _telegram.SendTextAsync(default!, default!, default, default!, default)
            .ThrowsAsyncForAnyArgs(new TelegramOperationException(new string('e', 2000)));

        await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(Command()));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.SingleAsync();
        Assert.Equal(PublishableEntity.MaxErrorTextLength, saved.ErrorText!.Length);
    }
}
