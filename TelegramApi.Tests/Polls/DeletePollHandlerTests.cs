using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.DeletePoll;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Polls;

public sealed class DeletePollHandlerTests : IDisposable
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
        await DeletePollHandler.Handle(
            new DeletePollCommand(id), db, _telegram, NullLogger<DeletePollHandler>.Instance, CancellationToken.None);
    }

    private async Task<Poll> Load(Guid id)
    {
        await using var db = _database.CreateContext();
        return (await db.Polls.FindAsync(id))!;
    }

    [Fact]
    public async Task Deletes_poll_message_and_marks_deleted()
    {
        var poll = await Seed();

        await Handle(poll.Id);

        await _telegram.Received(1).DeleteMessagesAsync(
            TestData.ChatId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 20 })),
            Arg.Any<CancellationToken>());
        var saved = await Load(poll.Id);
        Assert.Equal(PublicationStatus.Deleted, saved.Status);
        Assert.NotNull(saved.DeletedAt);
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_id()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));
    }

    [Fact]
    public async Task Does_nothing_for_already_deleted_poll()
    {
        var poll = await Seed(p => p.MarkDeleted(DateTime.UtcNow.AddDays(-1)));

        await Handle(poll.Id);

        await _telegram.DidNotReceiveWithAnyArgs().DeleteMessagesAsync(default, default!, default);
    }

    [Fact]
    public async Task Marks_failed_poll_deleted_without_calling_telegram()
    {
        var poll = await Seed(p => p.Status = PublicationStatus.Failed);

        await Handle(poll.Id);

        await _telegram.DidNotReceiveWithAnyArgs().DeleteMessagesAsync(default, default!, default);
        Assert.Equal(PublicationStatus.Deleted, (await Load(poll.Id)).Status);
    }
}
