using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.DeletePublication;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Publications;

public sealed class DeletePublicationHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ITelegramService _telegram = Substitute.For<ITelegramService>();

    public void Dispose() => _database.Dispose();

    private async Task Seed(Publication publication)
    {
        await using var db = _database.CreateContext();
        db.Publications.Add(publication);
        await db.SaveChangesAsync();
    }

    private async Task Handle(Guid id)
    {
        await using var db = _database.CreateContext();
        await DeletePublicationHandler.Handle(
            new DeletePublicationCommand(id), db, _telegram, NullLogger<DeletePublicationHandler>.Instance, CancellationToken.None);
    }

    private async Task<Publication> Load(Guid id)
    {
        await using var db = _database.CreateContext();
        return (await db.Publications.FindAsync(id))!;
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_id()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Handle(Guid.NewGuid()));
    }

    [Fact]
    public async Task Deletes_text_message_and_marks_deleted()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        await Seed(publication);

        await Handle(publication.Id);

        await _telegram.Received(1).DeleteMessagesAsync(
            TestData.ChatId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10 })),
            Arg.Any<CancellationToken>());
        var saved = await Load(publication.Id);
        Assert.Equal(PublicationStatus.Deleted, saved.Status);
        Assert.NotNull(saved.DeletedAt);
    }

    [Fact]
    public async Task Deletes_every_album_message_once()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        publication.TgMsgId = 100;
        publication.MediaFiles =
        [
            new MediaFile { FileName = "a.jpg", TgFileId = "f1", TgMessageId = 100, Order = 0 },
            new MediaFile { FileName = "b.jpg", TgFileId = "f2", TgMessageId = 101, Order = 1 },
            new MediaFile { FileName = "c.jpg", TgFileId = "f3", TgMessageId = 102, Order = 2 }
        ];
        await Seed(publication);

        await Handle(publication.Id);

        await _telegram.Received(1).DeleteMessagesAsync(
            TestData.ChatId,
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { 100, 101, 102 })),
            Arg.Any<CancellationToken>());
        Assert.Equal(PublicationStatus.Deleted, (await Load(publication.Id)).Status);
    }

    [Fact]
    public async Task Does_nothing_for_already_deleted_publication()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        publication.MarkDeleted(DateTime.UtcNow.AddDays(-1));
        var deletedAt = publication.DeletedAt;
        await Seed(publication);

        await Handle(publication.Id);

        await _telegram.DidNotReceiveWithAnyArgs().DeleteMessagesAsync(default, default!, default);
        Assert.Equal(deletedAt, (await Load(publication.Id)).DeletedAt);
    }

    [Fact]
    public async Task Marks_failed_publication_deleted_without_calling_telegram()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        publication.Status = PublicationStatus.Failed;
        publication.TgMsgId = 0;
        await Seed(publication);

        await Handle(publication.Id);

        await _telegram.DidNotReceiveWithAnyArgs().DeleteMessagesAsync(default, default!, default);
        Assert.Equal(PublicationStatus.Deleted, (await Load(publication.Id)).Status);
    }

    [Fact]
    public async Task Keeps_status_when_telegram_fails_to_delete()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        await Seed(publication);
        _telegram.DeleteMessagesAsync(default, default!, default)
            .ThrowsAsyncForAnyArgs(new TelegramOperationException("message can't be deleted", 400));

        await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(publication.Id));

        var saved = await Load(publication.Id);
        Assert.Equal(PublicationStatus.Sent, saved.Status);
        Assert.Null(saved.DeletedAt);
    }
}
