using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.PinPoll;
using TelegramApi.Application.Polls.UnpinPoll;
using TelegramApi.Application.Publications.PinPublication;
using TelegramApi.Application.Publications.UnpinPublication;
using TelegramApi.Domain;
using TelegramApi.Tests.Support;

namespace TelegramApi.Tests;

public sealed class PinTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly FakeTelegramService telegram = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task Publication_PinThenUnpin()
    {
        var id = await SeedPublicationAsync(PublicationStatus.Sent);

        await using (var db = database.CreateContext())
        {
            await PinPublicationHandler.Handle(
                new PinPublicationCommand(id, DisableNotification: true), db, telegram,
                NullLogger<PinPublicationHandler>.Instance, CancellationToken.None);
        }

        Assert.True(await IsPublicationPinnedAsync(id));

        await using (var db = database.CreateContext())
        {
            await UnpinPublicationHandler.Handle(
                new UnpinPublicationCommand(id), db, telegram,
                NullLogger<UnpinPublicationHandler>.Instance, CancellationToken.None);
        }

        Assert.False(await IsPublicationPinnedAsync(id));
        Assert.Equal(["Pin:42:True", "Unpin:42"], telegram.Calls);
    }

    [Fact]
    public async Task Publication_RepeatedPinCallsTelegramAgain()
    {
        var id = await SeedPublicationAsync(PublicationStatus.Sent);

        for (var i = 0; i < 2; i++)
        {
            await using var db = database.CreateContext();
            await PinPublicationHandler.Handle(
                new PinPublicationCommand(id, DisableNotification: false), db, telegram,
                NullLogger<PinPublicationHandler>.Instance, CancellationToken.None);
        }

        Assert.Equal(["Pin:42:False", "Pin:42:False"], telegram.Calls);
        Assert.True(await IsPublicationPinnedAsync(id));
    }

    [Theory]
    [InlineData(PublicationStatus.Failed)]
    [InlineData(PublicationStatus.Deleted)]
    public async Task Publication_NotSent_Conflicts(PublicationStatus status)
    {
        var id = await SeedPublicationAsync(status);

        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<ConflictException>(() => PinPublicationHandler.Handle(
            new PinPublicationCommand(id, false), db, telegram,
            NullLogger<PinPublicationHandler>.Instance, CancellationToken.None));
        Assert.Empty(telegram.Calls);
    }

    [Fact]
    public async Task Publication_Unknown_NotFound()
    {
        await using var db = database.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => UnpinPublicationHandler.Handle(
            new UnpinPublicationCommand(Guid.NewGuid()), db, telegram,
            NullLogger<UnpinPublicationHandler>.Instance, CancellationToken.None));
    }

    [Fact]
    public async Task Poll_PinThenUnpin()
    {
        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            TgAlias = "@test_channel",
            TgChatId = FakeTelegramService.ChatId,
            TgMsgId = 7,
            Status = PublicationStatus.Sent,
            Question = "Question?",
            CreatedAt = DateTime.UtcNow
        };

        await using (var db = database.CreateContext())
        {
            db.Polls.Add(poll);
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateContext())
        {
            await PinPollHandler.Handle(
                new PinPollCommand(poll.Id, false), db, telegram,
                NullLogger<PinPollHandler>.Instance, CancellationToken.None);
            Assert.True((await db.Polls.SingleAsync()).IsPinned);
        }

        await using (var db = database.CreateContext())
        {
            await UnpinPollHandler.Handle(
                new UnpinPollCommand(poll.Id), db, telegram,
                NullLogger<UnpinPollHandler>.Instance, CancellationToken.None);
            Assert.False((await db.Polls.SingleAsync()).IsPinned);
        }

        Assert.Equal(["Pin:7:False", "Unpin:7"], telegram.Calls);
    }

    [Fact]
    public void MarkDeleted_ClearsPinnedFlag()
    {
        var publication = new Publication { IsPinned = true };

        publication.MarkDeleted(DateTime.UtcNow);

        Assert.False(publication.IsPinned);
    }

    private async Task<Guid> SeedPublicationAsync(PublicationStatus status)
    {
        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            TgAlias = "@test_channel",
            TgChatId = FakeTelegramService.ChatId,
            TgMsgId = 42,
            Status = status,
            Text = "Hello",
            CreatedAt = DateTime.UtcNow
        };

        await using var db = database.CreateContext();
        db.Publications.Add(publication);
        await db.SaveChangesAsync();
        return publication.Id;
    }

    private async Task<bool> IsPublicationPinnedAsync(Guid id)
    {
        await using var db = database.CreateContext();
        return (await db.Publications.SingleAsync(p => p.Id == id)).IsPinned;
    }
}
