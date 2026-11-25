using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using TelegramApi.Application.Common;
using TelegramApi.Contracts;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class PublicationEndpointsTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task CreateText_SendsToTelegram_AndStoresSentRecord()
    {
        var channel = NewChannel();
        var response = await PostJsonAsync("/api/publications", new
        {
            channelAlias = channel.TrimStart('@'),
            text = "<b>News</b>",
            format = "Html",
            buttons = new[] { new { text = "Open", url = "https://example.com" } }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<PublicationResponse>(response);
        Assert.Equal($"/api/publications/{created.Id}", response.Headers.Location?.ToString());
        Assert.Equal(PublicationStatus.Sent, created.Status);
        Assert.Equal(channel, created.ChannelAlias);
        Assert.Equal(FakeTelegramService.ChatId, created.TgChatId);
        Assert.NotNull(created.SentAt);
        Assert.Single(created.Buttons);

        var loaded = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{created.Id}", Json);
        Assert.Equal("<b>News</b>", loaded!.Text);
        Assert.Equal(TextFormat.Html, loaded.Format);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.SendTextAsync)));
    }

    [Fact]
    public async Task CreateText_InvalidInput_Returns400ProblemDetails_WithoutCallingTelegram()
    {
        var response = await PostJsonAsync("/api/publications", new
        {
            channelAlias = "x",
            text = "",
            buttons = new[] { new { text = "Insecure", url = "http://example.com" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains("ChannelAlias", problem.Errors.Keys);
        Assert.Contains("Text", problem.Errors.Keys);
        Assert.Contains("Buttons[0].Url", problem.Errors.Keys);
        Assert.Empty(Telegram.Calls);
    }

    [Fact]
    public async Task CreateText_TelegramFailure_Returns502_AndKeepsFailedRecord()
    {
        Telegram.FailNextSend = new TelegramOperationException("Bad Gateway", 502);

        var response = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "Hi" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var problem = await ReadAsync<ProblemDetails>(response);
        var id = Guid.Parse(problem.Extensions["publicationId"]!.ToString()!);

        var stored = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{id}", Json);
        Assert.Equal(PublicationStatus.Failed, stored!.Status);
        Assert.Equal(502, stored.ErrorCode);
        Assert.Equal("Bad Gateway", stored.ErrorText);
        Assert.NotNull(stored.ErrorDate);
    }

    [Fact]
    public async Task CreateText_TelegramRejectsMarkup_Returns422()
    {
        Telegram.FailNextSend = new TelegramOperationException("Bad Request: can't parse entities", 400);

        var response = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "<b>", format = "Html" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateText_Timeout_Returns504()
    {
        Telegram.FailNextSend = new TelegramOperationException("Telegram request timed out.", isTimeout: true);

        var response = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "Hi" });

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task CreateText_UnknownChannel_Returns404_AndCreatesNoRecord()
    {
        var channel = NewChannel();
        Telegram.FailEnsureCanPublish = new ChannelNotFoundException("not found");

        var response = await PostJsonAsync("/api/publications", new { channelAlias = channel, text = "Hi" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var history = await Client.GetFromJsonAsync<PagedResponse<HistoryItemResponse>>(
            $"/api/publications-history?channelAlias={Uri.EscapeDataString(channel)}", Json);
        Assert.Equal(0, history!.TotalCount);
    }

    [Fact]
    public async Task CreateText_BotWithoutRights_Returns403()
    {
        Telegram.FailEnsureCanPublish = new BotLacksRightsException("no rights");

        var response = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "Hi" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPublication_Unknown_Returns404()
    {
        var response = await Client.GetAsync($"/api/publications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CreateMedia_SinglePhotoWithButton_StoresFileIdAndButton()
    {
        using var form = MediaForm(NewChannel(), files: 1, caption: "Photo");
        form.Add(new StringContent("Open"), "Buttons[0].Text");
        form.Add(new StringContent("https://example.com"), "Buttons[0].Url");

        var response = await Client.PostAsync("/api/media-publications", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<PublicationResponse>(response);
        var file = Assert.Single(created.MediaFiles);
        Assert.Equal(MediaType.Photo, file.MediaType);
        Assert.False(string.IsNullOrEmpty(file.TgFileId));
        Assert.Equal("https://example.com", Assert.Single(created.Buttons).Url);
        Assert.Equal("Photo", created.Text);
    }

    [Fact]
    public async Task CreateMedia_Album_KeepsClientOrder()
    {
        using var form = MediaForm(NewChannel(), files: 3, contentType: "application/pdf");

        var response = await Client.PostAsync("/api/media-publications", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync<PublicationResponse>(response);
        Assert.Equal(["file0.bin", "file1.bin", "file2.bin"], created.MediaFiles.Select(f => f.FileName));
        Assert.All(created.MediaFiles, f => Assert.Equal(MediaType.Document, f.MediaType));
    }

    [Fact]
    public async Task CreateMedia_AlbumWithButtons_Returns400()
    {
        using var form = MediaForm(NewChannel(), files: 2);
        form.Add(new StringContent("Open"), "Buttons[0].Text");
        form.Add(new StringContent("https://example.com"), "Buttons[0].Url");

        var response = await Client.PostAsync("/api/media-publications", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Telegram.Calls);
    }

    [Fact]
    public async Task CreateMedia_DisallowedType_Returns400()
    {
        using var form = MediaForm(NewChannel(), files: 1, contentType: "application/x-msdownload");

        var response = await Client.PostAsync("/api/media-publications", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_SentPublication_DeletesInTelegramOnce_AndIsIdempotent()
    {
        var created = await CreateTextAsync();
        Telegram.Reset();

        var first = await Client.DeleteAsync($"/api/publications/{created.Id}");
        var second = await Client.DeleteAsync($"/api/publications/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.DeleteMessagesAsync)));

        var stored = await Client.GetFromJsonAsync<PublicationResponse>($"/api/publications/{created.Id}", Json);
        Assert.Equal(PublicationStatus.Deleted, stored!.Status);
        Assert.NotNull(stored.DeletedAt);
    }

    [Fact]
    public async Task Delete_FailedPublication_DoesNotCallTelegram()
    {
        Telegram.FailNextSend = new TelegramOperationException("Bad Gateway", 502);
        var failed = await PostJsonAsync("/api/publications", new { channelAlias = NewChannel(), text = "Hi" });
        var id = Guid.Parse((await ReadAsync<ProblemDetails>(failed)).Extensions["publicationId"]!.ToString()!);
        Telegram.Reset();

        var response = await Client.DeleteAsync($"/api/publications/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(Telegram.Calls);
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var response = await Client.DeleteAsync($"/api/publications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
