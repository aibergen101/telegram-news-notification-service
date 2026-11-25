using TelegramApi.Application.Common;
using TelegramApi.Application.History.GetHistory;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.History;

public sealed class GetHistoryHandlerTests : IDisposable
{
    private static readonly DateTime Day1 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = Day1.AddDays(1);
    private static readonly DateTime Day3 = Day1.AddDays(2);
    private static readonly DateTime Day4 = Day1.AddDays(3);

    private readonly TestDatabase _database = new();

    public GetHistoryHandlerTests()
    {
        var failedPublication = TestData.SentPublication(Day3, text: "Failed post");
        failedPublication.Status = PublicationStatus.Failed;

        var deletedPoll = TestData.SentPoll(Day4, alias: "@other_channel", question: "Deleted poll");
        deletedPoll.MarkDeleted(Day4);

        using var db = _database.CreateContext();
        db.Publications.AddRange(
            TestData.SentPublication(Day1, text: "First post"),
            failedPublication);
        db.Polls.AddRange(
            TestData.SentPoll(Day2, question: "First poll"),
            deletedPoll);
        db.SaveChanges();
    }

    public void Dispose() => _database.Dispose();

    private async Task<PagedResult<HistoryItemResult>> Handle(
        int page = 1,
        int pageSize = 20,
        PublicationStatus? status = null,
        string? alias = null,
        DateTime? from = null,
        DateTime? to = null)
    {
        await using var db = _database.CreateContext();
        return await GetHistoryHandler.Handle(
            new GetHistoryQuery(page, pageSize, status, alias, from, to), db, CancellationToken.None);
    }

    [Fact]
    public async Task Returns_publications_and_polls_newest_first()
    {
        var result = await Handle();

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(["Deleted poll", "Failed post", "First poll", "First post"], result.Items.Select(i => i.Text));
        Assert.Equal(
            [PublicationKind.Poll, PublicationKind.Post, PublicationKind.Poll, PublicationKind.Post],
            result.Items.Select(i => i.Kind));
    }

    [Fact]
    public async Task Pages_results_and_keeps_total_count()
    {
        var page1 = await Handle(page: 1, pageSize: 3);
        var page2 = await Handle(page: 2, pageSize: 3);

        Assert.Equal(3, page1.Items.Count);
        Assert.Single(page2.Items);
        Assert.Equal(4, page1.TotalCount);
        Assert.Equal(4, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(3, page2.PageSize);
        Assert.Equal("First post", page2.Items[0].Text);
    }

    [Fact]
    public async Task Filters_by_status()
    {
        var result = await Handle(status: PublicationStatus.Sent);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, i => Assert.Equal(PublicationStatus.Sent, i.Status));
    }

    [Theory]
    [InlineData("other_channel")]
    [InlineData("@other_channel")]
    public async Task Filters_by_channel_alias_with_or_without_at(string alias)
    {
        var result = await Handle(alias: alias);

        var item = Assert.Single(result.Items);
        Assert.Equal("Deleted poll", item.Text);
        Assert.Equal(PublicationStatus.Deleted, item.Status);
        Assert.NotNull(item.DeletedAt);
    }

    [Fact]
    public async Task Filters_by_date_range_inclusive()
    {
        var result = await Handle(from: Day2, to: Day3);

        Assert.Equal(["Failed post", "First poll"], result.Items.Select(i => i.Text));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Returns_empty_page_beyond_last()
    {
        var result = await Handle(page: 5, pageSize: 20);

        Assert.Empty(result.Items);
        Assert.Equal(4, result.TotalCount);
    }
}
