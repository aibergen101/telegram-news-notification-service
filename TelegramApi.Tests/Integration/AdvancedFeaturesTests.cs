using System.Net;
using System.Net.Http.Json;
using TelegramApi.Application.Common;
using TelegramApi.Contracts;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Integration;

/// <summary>Дополнительные задания ТЗ 15: редактирование, закрепление, идемпотентность.</summary>
[Collection(ApiCollection.Name)]
public sealed class AdvancedFeaturesTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Edit_TextPublication_ReplacesTextAndButtons()
    {
        var created = await CreateTextAsync();
        Telegram.Reset();

        var response = await Client.PutAsJsonAsync($"/api/publications/{created.Id}", new
        {
            text = "Updated",
            format = "MarkdownV2",
            buttons = new[] { new { text = "Docs", url = "https://example.com/docs" } }
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await ReadAsync<PublicationResponse>(response);
        Assert.Equal("Updated", edited.Text);
        Assert.Equal(TextFormat.MarkdownV2, edited.Format);
        Assert.Equal("Docs", Assert.Single(edited.Buttons).Text);
        Assert.NotNull(edited.EditedAt);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.EditTextAsync)));

        var stored = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{created.Id}", Json);
        Assert.Equal("Updated", stored!.Text);
        Assert.Single(stored.Buttons);
    }

    [Fact]
    public async Task Edit_MediaPublication_EditsCaption()
    {
        using var form = MediaForm(NewChannel(), files: 1, caption: "old");
        var created = await ReadAsync<PublicationResponse>(await Client.PostAsync("/api/media-publications", form));
        Telegram.Reset();

        var response = await Client.PutAsJsonAsync($"/api/publications/{created.Id}", new { text = (string?)null }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await ReadAsync<PublicationResponse>(response)).Text);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.EditCaptionAsync)));
    }

    [Fact]
    public async Task Edit_TextPublicationWithEmptyText_Returns400()
    {
        var created = await CreateTextAsync();

        var response = await Client.PutAsJsonAsync($"/api/publications/{created.Id}", new { text = "" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_AlbumWithButtons_Returns400()
    {
        using var form = MediaForm(NewChannel(), files: 2);
        var created = await ReadAsync<PublicationResponse>(await Client.PostAsync("/api/media-publications", form));

        var response = await Client.PutAsJsonAsync($"/api/publications/{created.Id}", new
        {
            text = "caption",
            buttons = new[] { new { text = "Open", url = "https://example.com" } }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Edit_DeletedPublication_Returns409()
    {
        var created = await CreateTextAsync();
        await Client.DeleteAsync($"/api/publications/{created.Id}");

        var response = await Client.PutAsJsonAsync($"/api/publications/{created.Id}", new { text = "x" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PinAndUnpin_Publication_AreIdempotent()
    {
        var created = await CreateTextAsync();
        Telegram.Reset();

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsync($"/api/publications/{created.Id}/pin", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PutAsync($"/api/publications/{created.Id}/pin", null)).StatusCode);
        var pinned = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{created.Id}", Json);
        Assert.True(pinned!.IsPinned);
        Assert.NotNull(pinned.PinnedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/publications/{created.Id}/pin")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/publications/{created.Id}/pin")).StatusCode);
        var unpinned = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{created.Id}", Json);
        Assert.False(unpinned!.IsPinned);

        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.PinMessageAsync)));
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.UnpinMessageAsync)));
    }

    [Fact]
    public async Task Pin_Poll_Works_AndDeletedPollReturns409()
    {
        var poll = await CreatePollAsync();

        var pin = await Client.PutAsync($"/api/polls/{poll.Id}/pin?disableNotification=true", null);
        Assert.Equal(HttpStatusCode.NoContent, pin.StatusCode);
        Assert.True((await Client.GetFromJsonAsync<PollResponse>($"/api/polls/{poll.Id}", Json))!.IsPinned);

        await Client.DeleteAsync($"/api/polls/{poll.Id}");
        var afterDelete = await Client.GetFromJsonAsync<PollResponse>($"/api/polls/{poll.Id}", Json);
        Assert.False(afterDelete!.IsPinned);

        var response = await Client.PutAsync($"/api/polls/{poll.Id}/pin", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Pin_Unknown_Returns404()
    {
        var response = await Client.PutAsync($"/api/publications/{Guid.NewGuid()}/pin", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Idempotency_SameKeySameRequest_ReturnsExistingRecordWithoutRepublishing()
    {
        var key = Guid.NewGuid().ToString();
        var body = new { channelAlias = NewChannel(), text = "Once" };

        var first = await PostJsonAsync("/api/publications", body, key);
        var second = await PostJsonAsync("/api/publications", body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("true", second.Headers.GetValues("Idempotent-Replayed").Single());
        Assert.Equal((await ReadAsync<PublicationResponse>(first)).Id, (await ReadAsync<PublicationResponse>(second)).Id);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.SendTextAsync)));
    }

    [Fact]
    public async Task Idempotency_SameKeyDifferentRequest_Returns409()
    {
        var key = Guid.NewGuid().ToString();
        var channel = NewChannel();

        await PostJsonAsync("/api/publications", new { channelAlias = channel, text = "One" }, key);
        var conflict = await PostJsonAsync("/api/publications", new { channelAlias = channel, text = "Two" }, key);
        var otherKind = await PostJsonAsync("/api/polls", new
        {
            channelAlias = channel,
            question = "Q?",
            options = new[] { new { text = "A" }, new { text = "B" } }
        }, key);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, otherKind.StatusCode);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.SendTextAsync)));
        Assert.Equal(0, Telegram.CountOf(nameof(ITelegramService.SendPollAsync)));
    }

    [Fact]
    public async Task Idempotency_Poll_ReplaysExistingPoll()
    {
        var key = Guid.NewGuid().ToString();
        var body = new { channelAlias = NewChannel(), question = "Q?", options = new[] { new { text = "A" }, new { text = "B" } } };

        var first = await ReadAsync<PollResponse>(await PostJsonAsync("/api/polls", body, key));
        var second = await PostJsonAsync("/api/polls", body, key);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(first.Id, (await ReadAsync<PollResponse>(second)).Id);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.SendPollAsync)));
    }

    [Fact]
    public async Task Idempotency_Media_ComparesFileContent()
    {
        var key = Guid.NewGuid().ToString();
        var channel = NewChannel();

        using var firstForm = MediaForm(channel, files: 1);
        using var sameForm = MediaForm(channel, files: 1);

        var first = await SendMediaAsync(firstForm, key);
        var replay = await SendMediaAsync(sameForm, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.SendMediaAsync)));

        using var changedContent = new MultipartFormDataContent
        {
            { new StringContent(channel), "ChannelAlias" }
        };
        var file = new ByteArrayContent([9, 9, 9]);
        file.Headers.ContentType = new("image/png");
        changedContent.Add(file, "Files", "file0.bin");

        var conflict = await SendMediaAsync(changedContent, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Idempotency_InvalidKey_Returns400()
    {
        var response = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "x" }, new string('k', 300));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendMediaAsync(MultipartFormDataContent form, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/media-publications") { Content = form };
        request.Headers.Add("Idempotency-Key", key);
        return Client.SendAsync(request);
    }
}
