using System.Text;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Unit;

public sealed class DomainAndRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NewRecord_IsFailedUntilTelegramConfirms()
    {
        Assert.Equal(PublicationStatus.Failed, new Publication().Status);
    }

    [Fact]
    public void MarkSent_StoresTelegramIdentifiers()
    {
        var publication = new Publication();

        publication.MarkSent(-100, 42, Now);

        Assert.Equal(PublicationStatus.Sent, publication.Status);
        Assert.Equal(-100, publication.TgChatId);
        Assert.Equal(42, publication.TgMsgId);
        Assert.Equal(Now, publication.SentAt);
    }

    [Fact]
    public void MarkFailed_TruncatesLongErrorText()
    {
        var poll = new Poll();

        poll.MarkFailed(400, new string('x', 5000), Now);

        Assert.Equal(PublishableEntity.MaxErrorTextLength, poll.ErrorText!.Length);
        Assert.Equal(400, poll.ErrorCode);
        Assert.Equal(Now, poll.ErrorDate);
    }

    [Fact]
    public void MarkDeleted_AlsoClearsPin()
    {
        var publication = new Publication();
        publication.MarkPinned(Now);

        publication.MarkDeleted(Now);

        Assert.Equal(PublicationStatus.Deleted, publication.Status);
        Assert.False(publication.IsPinned);
        Assert.Null(publication.PinnedAt);
        Assert.Equal(Now, publication.DeletedAt);
    }

    [Fact]
    public void MarkEdited_ReplacesContent()
    {
        var publication = new Publication { Text = "old", Buttons = [new Button { Text = "a", Url = "https://a" }] };

        publication.MarkEdited("new", null, TextFormat.Html, [], Now);

        Assert.Equal("new", publication.Text);
        Assert.Equal(TextFormat.Html, publication.Format);
        Assert.Empty(publication.Buttons);
        Assert.Equal(Now, publication.EditedAt);
    }

    [Fact]
    public void AlbumDetection()
    {
        var single = new Publication { MediaFiles = [new MediaFile()] };
        var album = new Publication { MediaFiles = [new MediaFile(), new MediaFile()] };

        Assert.False(new Publication().HasMedia);
        Assert.True(single.HasMedia);
        Assert.False(single.IsAlbum);
        Assert.True(album.IsAlbum);
    }

    [Theory]
    [InlineData("image/jpeg", MediaType.Photo)]
    [InlineData("IMAGE/PNG", MediaType.Photo)]
    [InlineData("image/gif", MediaType.Document)]
    [InlineData("application/pdf", MediaType.Document)]
    public void MediaRules_Classify(string contentType, MediaType expected)
    {
        Assert.Equal(expected, MediaRules.Classify(contentType));
        Assert.True(MediaRules.IsAllowed(contentType));
    }

    [Theory]
    [InlineData("my_channel", "@my_channel")]
    [InlineData(" @my_channel ", "@my_channel")]
    public void ChannelAlias_Normalize(string input, string expected)
    {
        Assert.Equal(expected, ChannelAlias.Normalize(input));
    }

    [Fact]
    public void IdempotencyHash_DependsOnContent()
    {
        var a = Idempotency.ComputeHash(new { Text = "a", Buttons = new[] { new ButtonDto("x", "https://x") } });
        var sameAsA = Idempotency.ComputeHash(new { Text = "a", Buttons = new[] { new ButtonDto("x", "https://x") } });
        var b = Idempotency.ComputeHash(new { Text = "b", Buttons = new[] { new ButtonDto("x", "https://x") } });

        Assert.Equal(a, sameAsA);
        Assert.NotEqual(a, b);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public async Task IdempotencyStreamHash_RewindsStreamForSending()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("file content"));

        var first = await Idempotency.HashStreamAsync(stream, CancellationToken.None);
        var second = await Idempotency.HashStreamAsync(stream, CancellationToken.None);

        Assert.Equal(0, stream.Position);
        Assert.Equal(first, second);
    }
}
