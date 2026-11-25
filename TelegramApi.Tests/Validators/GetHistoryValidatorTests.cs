using TelegramApi.Application.History.GetHistory;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Validators;

public class GetHistoryValidatorTests
{
    private readonly GetHistoryValidator _validator = new();

    private static GetHistoryQuery Query(
        int page = 1,
        int pageSize = HistoryLimits.DefaultPageSize,
        PublicationStatus? status = null,
        string? alias = null,
        DateTime? from = null,
        DateTime? to = null) =>
        new(page, pageSize, status, alias, from, to);

    [Fact]
    public void Accepts_default_query()
    {
        Assert.True(_validator.Validate(Query()).IsValid);
    }

    [Fact]
    public void Accepts_all_filters()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _validator.Validate(Query(status: PublicationStatus.Sent, alias: "my_channel", from: from, to: from));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_page_below_one(int page)
    {
        var result = _validator.Validate(Query(page: page));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetHistoryQuery.Page));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(HistoryLimits.MaxPageSize, true)]
    [InlineData(HistoryLimits.MaxPageSize + 1, false)]
    public void Checks_page_size(int pageSize, bool expectedValid)
    {
        Assert.Equal(expectedValid, _validator.Validate(Query(pageSize: pageSize)).IsValid);
    }

    [Fact]
    public void Rejects_unknown_status()
    {
        var result = _validator.Validate(Query(status: (PublicationStatus)42));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetHistoryQuery.Status));
    }

    [Fact]
    public void Rejects_invalid_alias_but_allows_blank()
    {
        Assert.False(_validator.Validate(Query(alias: "@x")).IsValid);
        Assert.True(_validator.Validate(Query(alias: "   ")).IsValid);
    }

    [Fact]
    public void Rejects_to_earlier_than_from()
    {
        var from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var result = _validator.Validate(Query(from: from, to: from.AddDays(-1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(GetHistoryQuery.To));
    }
}
