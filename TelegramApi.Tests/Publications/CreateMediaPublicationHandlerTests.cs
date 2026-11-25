using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Publications;

public sealed class CreateMediaPublicationHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly ITelegramService _telegram = Substitute.For<ITelegramService>();

    public void Dispose() => _database.Dispose();

    private async Task<PublicationDetails> Handle(CreateMediaPublicationCommand command)
    {
        await using var db = _database.CreateContext();
        return await CreateMediaPublicationHandler.Handle(
            command, db, _telegram, NullLogger<CreateMediaPublicationHandler>.Instance, CancellationToken.None);
    }

    private static CreateMediaPublicationCommand AlbumCommand() =>
        new(
            TestData.Alias,
            "Caption",
            TextFormat.Plain,
            [TestData.File(fileName: "one.jpg"), TestData.File(fileName: "two.jpg")]);

    [Fact]
    public async Task Sends_album_and_saves_every_file()
    {
        _telegram.SendMediaAsync(default!, default, default, default!, default)
            .ReturnsForAnyArgs(new List<MediaSendResult>
            {
                new(100, TestData.ChatId, MediaType.Photo, "file-1"),
                new(101, TestData.ChatId, MediaType.Photo, "file-2")
            });

        var result = await Handle(AlbumCommand());

        await _telegram.Received(1).SendMediaAsync(
            TestData.Alias,
            "Caption",
            TextFormat.Plain,
            Arg.Is<IReadOnlyList<MediaFileDto>>(f => f.Count == 2),
            Arg.Any<CancellationToken>());

        Assert.Equal(PublicationStatus.Sent, result.Status);
        Assert.Equal(100, result.TgMessageId);
        Assert.Equal(TestData.ChatId, result.TgChatId);
        Assert.Collection(
            result.MediaFiles,
            f =>
            {
                Assert.Equal("one.jpg", f.FileName);
                Assert.Equal("file-1", f.TgFileId);
                Assert.Equal(100, f.TgMessageId);
                Assert.Equal(0, f.Order);
            },
            f =>
            {
                Assert.Equal("two.jpg", f.FileName);
                Assert.Equal("file-2", f.TgFileId);
                Assert.Equal(101, f.TgMessageId);
                Assert.Equal(1, f.Order);
            });

        await using var check = _database.CreateContext();
        var saved = await check.Publications.Include(p => p.MediaFiles).SingleAsync();
        Assert.Equal(PublicationStatus.Sent, saved.Status);
        Assert.Equal(2, saved.MediaFiles.Count);
    }

    [Fact]
    public async Task Saves_failed_publication_without_files_when_telegram_fails()
    {
        _telegram.SendMediaAsync(default!, default, default, default!, default)
            .ThrowsAsyncForAnyArgs(new TelegramOperationException("Request timed out", isTimeout: true));

        var thrown = await Assert.ThrowsAsync<TelegramOperationException>(() => Handle(AlbumCommand()));

        await using var check = _database.CreateContext();
        var saved = await check.Publications.Include(p => p.MediaFiles).SingleAsync();
        Assert.Equal(saved.Id, thrown.RecordId);
        Assert.Equal(PublicationStatus.Failed, saved.Status);
        Assert.Equal("Request timed out", saved.ErrorText);
        Assert.Empty(saved.MediaFiles);
    }

    [Fact]
    public async Task Does_not_create_record_when_bot_lacks_rights()
    {
        _telegram.EnsureCanPublishAsync(default!, default)
            .ThrowsAsyncForAnyArgs(new BotLacksRightsException("no rights"));

        await Assert.ThrowsAsync<BotLacksRightsException>(() => Handle(AlbumCommand()));

        await _telegram.DidNotReceiveWithAnyArgs().SendMediaAsync(default!, default, default, default!, default);
        await using var check = _database.CreateContext();
        Assert.Empty(check.Publications);
    }
}
