using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.GetPublication;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Publications;

public sealed class GetPublicationHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Returns_publication_with_ordered_buttons_and_files()
    {
        var publication = TestData.SentPublication(DateTime.UtcNow);
        publication.Buttons =
        [
            new Button { Id = Guid.NewGuid(), Text = "Second", Url = "https://b.example", Order = 1 },
            new Button { Id = Guid.NewGuid(), Text = "First", Url = "https://a.example", Order = 0 }
        ];
        publication.MediaFiles =
        [
            new MediaFile { FileName = "b.pdf", MediaType = MediaType.Document, TgFileId = "f2", TgMessageId = 11, Order = 1 },
            new MediaFile { FileName = "a.pdf", MediaType = MediaType.Document, TgFileId = "f1", TgMessageId = 10, Order = 0 }
        ];

        await using (var seed = _database.CreateContext())
        {
            seed.Publications.Add(publication);
            await seed.SaveChangesAsync();
        }

        await using var db = _database.CreateContext();
        var result = await GetPublicationHandler.Handle(new GetPublicationQuery(publication.Id), db, CancellationToken.None);

        Assert.Equal(publication.Id, result.Id);
        Assert.Equal(TestData.Alias, result.ChannelAlias);
        Assert.Equal(PublicationStatus.Sent, result.Status);
        Assert.Equal("Hello", result.Text);
        Assert.Equal(["First", "Second"], result.Buttons.Select(b => b.Text));
        Assert.Equal(["a.pdf", "b.pdf"], result.MediaFiles.Select(f => f.FileName));
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_id()
    {
        await using var db = _database.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            GetPublicationHandler.Handle(new GetPublicationQuery(Guid.NewGuid()), db, CancellationToken.None));
    }
}
