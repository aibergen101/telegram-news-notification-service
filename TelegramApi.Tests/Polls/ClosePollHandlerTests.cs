using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.ClosePoll;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Polls;

public sealed class ClosePollHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ITelegramService _telegram = Substitute.For<ITelegramService>();

    public void Dispose() => _database.Dispose();

    private async Task<Poll> Seed(Action<Poll>? change = null)
    {
        var poll = TestData.SentPoll(DateTime.UtcNow);
        change?.Invoke(poll);
        await using var db = _database.CreateContext();
        db.Polls.Add(poll);
        await db.SaveChangesAsync();
        return poll;
    }

    private async Task Handle(Guid id)
    {
        await using var db = _database.CreateContext();
        await ClosePollHandler.Handle(
            new ClosePollCommand(id), db, _telegram, NullLogger<ClosePollHandler>.Instance, CancellationToken.None);
    }

    private async Task<Poll> Load(Guid id)
    {
        await using var db = _database.CreateContext();
        return (await db.Polls.FindAsync(id))!;
    }

    [Fact]
    public async Task Stops_poll_and_marks_closed()
    {
        var poll = await Seed();

        await Handle(poll.Id);

        await _telegram.Received(1).StopPollAsync(TestData.ChatId, 20, Arg.Any<CancellationToken>());
        var saved = await Load(poll.Id);
        Assert.True(saved.IsClosed);
        Assert.NotNull(saved.ClosedAt);
        Assert.Equal(PublicationStatus.Sent, saved.Status);
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_id()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(PublicationStatus.Failed)]
    [InlineData(PublicationStatus.Deleted)]
    public async Task Throws_conflict_when_poll_is_not_sent(PublicationStatus status)
    {
        var poll = await Seed(p => p.Status = status);

        await Assert.ThrowsAsync<ConflictException>(() => Handle(poll.Id));

        await _telegram.DidNotReceiveWithAnyArgs().StopPollAsync(default, default, default);
    }

    [Fact]
    public async Task Does_nothing_for_already_closed_poll()
    {
        var poll = await Seed(p => p.MarkClosed(DateTime.UtcNow.AddHours(-1)));

        await Handle(poll.Id);

        await _telegram.DidNotReceiveWithAnyArgs().StopPollAsync(default, default, default);
    }

    [Fact]
    public async Task Leaves_poll_open_when_telegram_fails()
    {
        var poll = await Seed();
        _telegram.StopPollAsync(default, default, default)
            .ThrowsAsyncForAnyArgs(new TelegramOperationException("Telegram is unavailable"));

        await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(poll.Id));

        var saved = await Load(poll.Id);
        Assert.False(saved.IsClosed);
        Assert.Null(saved.ClosedAt);
    }
}
