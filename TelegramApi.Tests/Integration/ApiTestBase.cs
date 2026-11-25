using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramApi.Contracts;

namespace TelegramApi.Tests.Integration;

public abstract class ApiTestBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
        Telegram = factory.Telegram;
        Telegram.Reset();
        Client = factory.CreateClient();
    }

    protected ApiFactory Factory { get; }

    protected FakeTelegramService Telegram { get; }

    protected HttpClient Client { get; }

    /// <summary>Свой канал на каждый тест, чтобы фильтры истории не пересекались между тестами.</summary>
    protected static string NewChannel() => "@chan_" + Guid.NewGuid().ToString("N")[..12];

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    protected Task<HttpResponseMessage> PostJsonAsync(string url, object body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Json) };
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return Client.SendAsync(request);
    }

    protected async Task<PublicationResponse> CreateTextAsync(string? channel = null, string text = "Hello")
    {
        var response = await PostJsonAsync(
            "/api/publications",
            new { channelAlias = channel ?? NewChannel(), text });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PublicationResponse>(response);
    }

    protected async Task<PollResponse> CreatePollAsync(string? channel = null)
    {
        var response = await PostJsonAsync(
            "/api/polls",
            new { channelAlias = channel ?? NewChannel(), question = "Q?", options = new[] { new { text = "A" }, new { text = "B" } } });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PollResponse>(response);
    }

    protected static MultipartFormDataContent MediaForm(string channel, int files, string contentType = "image/png", string? caption = null)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(channel), "ChannelAlias" }
        };

        if (caption is not null)
        {
            form.Add(new StringContent(caption), "Text");
        }

        for (var i = 0; i < files; i++)
        {
            var file = new ByteArrayContent([1, 2, 3, (byte)i]);
            file.Headers.ContentType = new(contentType);
            form.Add(file, "Files", $"file{i}.bin");
        }

        return form;
    }
}
