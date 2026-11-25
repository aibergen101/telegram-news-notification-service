using System.Net;
using System.Net.Http.Json;
using TelegramApi.Application.Common;
using TelegramApi.Contracts;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class PollAndHistoryTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task CreatePoll_StoresOptionsInOrder()
    {
        var response = await PostJsonAsync("/api/polls", new
        {
            channelAlias = NewChannel(),
            question = "Best day?",
            options = new[] { new { text = "Mon" }, new { text = "Tue" }, new { text = "Wed" } },
            allowMultipleAnswers = true,
            isAnonymous = false,
            duration = 60
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var poll = await ReadAsync<PollResponse>(response);
        Assert.Equal(["Mon", "Tue", "Wed"], poll.Options.OrderBy(o => o.Order).Select(o => o.Text));
        Assert.True(poll.AllowMultipleAnswers);
        Assert.False(poll.IsAnonymous);
        Assert.Equal(60, poll.Duration);
        Assert.Equal(PublicationStatus.Sent, poll.Status);
    }

    [Fact]
    public async Task CreatePoll_InvalidDurationAndOptions_Returns400()
    {
        var response = await PostJsonAsync("/api/polls", new
        {
            channelAlias = NewChannel(),
            question = "Q?",
            options = new[] { new { text = "Only one" } },
            duration = 1000
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClosePoll_IsIdempotent_AndCallsTelegramOnce()
    {
        var poll = await CreatePollAsync();
        Telegram.Reset();

        var first = await Client.PostAsync($"/api/polls/{poll.Id}/close", null);
        var second = await Client.PostAsync($"/api/polls/{poll.Id}/close", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(1, Telegram.CountOf(nameof(ITelegramService.StopPollAsync)));

        var stored = await Client.GetFromJsonAsync<PollResponse>($"/api/polls/{poll.Id}", Json);
        Assert.True(stored!.IsClosed);
        Assert.NotNull(stored.ClosedAt);
    }

    [Fact]
    public async Task ClosePoll_AfterDelete_Returns409()
    {
        var poll = await CreatePollAsync();
        await Client.DeleteAsync($"/api/polls/{poll.Id}");

        var response = await Client.PostAsync($"/api/polls/{poll.Id}/close", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeletePoll_MarksDeleted()
    {
        var poll = await CreatePollAsync();

        var response = await Client.DeleteAsync($"/api/polls/{poll.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var stored = await Client.GetFromJsonAsync<PollResponse>($"/api/polls/{poll.Id}", Json);
        Assert.Equal(PublicationStatus.Deleted, stored!.Status);
    }

    [Fact]
    public async Task History_ContainsPostsAndPolls_NewestFirst_WithFiltersAndPaging()
    {
        var channel = NewChannel();
        var post = await CreateTextAsync(channel, "first");
        var poll = await CreatePollAsync(channel);
        var deleted = await CreateTextAsync(channel, "to delete");
        await Client.DeleteAsync($"/api/publications/{deleted.Id}");
        await CreateTextAsync(); // другой канал не должен попасть в выборку

        var all = await GetHistoryAsync($"channelAlias={Uri.EscapeDataString(channel)}");
        Assert.Equal(3, all.TotalCount);
        Assert.Equal([deleted.Id, poll.Id, post.Id], all.Items.Select(i => i.Id));
        Assert.Equal(PublicationKind.Poll, all.Items[1].Type);
        Assert.Equal("Q?", all.Items[1].Text);

        var sentOnly = await GetHistoryAsync($"channelAlias={Uri.EscapeDataString(channel)}&status=Sent");
        Assert.Equal(2, sentOnly.TotalCount);

        var page2 = await GetHistoryAsync($"channelAlias={Uri.EscapeDataString(channel)}&page=2&pageSize=2");
        Assert.Equal(post.Id, Assert.Single(page2.Items).Id);
        Assert.Equal(3, page2.TotalCount);

        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var none = await GetHistoryAsync($"channelAlias={Uri.EscapeDataString(channel)}&from={future}");
        Assert.Equal(0, none.TotalCount);

        var past = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
        var range = await GetHistoryAsync($"channelAlias={Uri.EscapeDataString(channel)}&from={past}&to={future}");
        Assert.Equal(3, range.TotalCount);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")]
    public async Task History_InvalidQuery_Returns400(string query)
    {
        var response = await Client.GetAsync($"/api/publications-history?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BotCheckAndChannelInfo_UseTelegram()
    {
        var check = await Client.GetAsync("/api/bot/check");
        var channel = await Client.GetFromJsonAsync<ChannelInfoResponse>("/api/channels/my_channel", Json);

        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.Equal("Channel", channel!.Type);
    }

    [Fact]
    public async Task HealthChecks_MainChecksDatabase_TelegramIsSeparate()
    {
        var health = await Client.GetAsync("/health");
        Telegram.FailNextSend = new TelegramOperationException("down", 502);
        var telegram = await Client.GetAsync("/health/telegram");
        Telegram.FailNextSend = new TelegramOperationException("down", 502);
        var healthWhileTelegramDown = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, telegram.StatusCode);
        Assert.Equal(HttpStatusCode.OK, healthWhileTelegramDown.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesAllEndpoints()
    {
        var document = await Client.GetStringAsync("/openapi/v1.json");

        foreach (var path in new[]
                 {
                     "/api/publications", "/api/media-publications", "/api/publications/{id}",
                     "/api/publications/{id}/pin", "/api/polls", "/api/polls/{id}/close", "/api/polls/{id}/pin",
                     "/api/publications-history", "/api/bot/check", "/api/channels/{alias}"
                 })
        {
            Assert.Contains($"\"{path}\"", document, StringComparison.Ordinal);
        }
    }

    private async Task<PagedResponse<HistoryItemResponse>> GetHistoryAsync(string query) =>
        (await Client.GetFromJsonAsync<PagedResponse<HistoryItemResponse>>($"/api/publications-history?{query}", Json))!;
}
