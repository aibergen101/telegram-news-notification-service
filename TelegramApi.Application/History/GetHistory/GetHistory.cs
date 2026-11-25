using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.History.GetHistory;

public static class HistoryLimits
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
}

public record GetHistoryQuery(
    int Page,
    int PageSize,
    PublicationStatus? Status,
    string? ChannelAlias,
    DateTime? From,
    DateTime? To);

/// <summary>Общая строка для объединения публикаций и опросов в одном SQL-запросе.</summary>
internal sealed class HistoryRow
{
    public Guid Id { get; set; }

    public PublicationKind Kind { get; set; }

    public string ChannelAlias { get; set; } = string.Empty;

    public string? Text { get; set; }

    public PublicationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}

public class GetHistoryValidator : AbstractValidator<GetHistoryQuery>
{
    public GetHistoryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize).InclusiveBetween(1, HistoryLimits.MaxPageSize);

        RuleFor(x => x.Status).IsInEnum();

        RuleFor(x => x.ChannelAlias)
            .Matches(ChannelAlias.Pattern)
            .When(x => !string.IsNullOrWhiteSpace(x.ChannelAlias))
            .WithMessage("Channel alias must look like @my_channel.");

        RuleFor(x => x.To)
            .Must((query, to) => query.From is null || to is null || to >= query.From)
            .WithMessage("'to' must not be earlier than 'from'.");
    }
}

public class GetHistoryHandler
{
    public static async Task<PagedResult<HistoryItemResult>> Handle(
        GetHistoryQuery query,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var alias = string.IsNullOrWhiteSpace(query.ChannelAlias) ? null : ChannelAlias.Normalize(query.ChannelAlias);

        var publications = db.Publications.AsNoTracking().AsQueryable();
        var polls = db.Polls.AsNoTracking().AsQueryable();

        if (query.Status is { } status)
        {
            publications = publications.Where(p => p.Status == status);
            polls = polls.Where(p => p.Status == status);
        }

        if (alias is not null)
        {
            publications = publications.Where(p => p.TgAlias == alias);
            polls = polls.Where(p => p.TgAlias == alias);
        }

        if (query.From is { } fromDate)
        {
            publications = publications.Where(p => p.CreatedAt >= fromDate);
            polls = polls.Where(p => p.CreatedAt >= fromDate);
        }

        if (query.To is { } toDate)
        {
            publications = publications.Where(p => p.CreatedAt <= toDate);
            polls = polls.Where(p => p.CreatedAt <= toDate);
        }

        var publicationRows = publications.Select(p => new HistoryRow
        {
            Id = p.Id,
            Kind = PublicationKind.Post,
            ChannelAlias = p.TgAlias,
            Text = p.Text,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            SentAt = p.SentAt,
            DeletedAt = p.DeletedAt
        });

        var pollRows = polls.Select(p => new HistoryRow
        {
            Id = p.Id,
            Kind = PublicationKind.Poll,
            ChannelAlias = p.TgAlias,
            Text = p.Question,
            Status = p.Status,
            CreatedAt = p.CreatedAt,
            SentAt = p.SentAt,
            DeletedAt = p.DeletedAt
        });

        var all = publicationRows.Concat(pollRows);

        var total = await all.CountAsync(ct);
        var rows = await all
            .OrderByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(r => new HistoryItemResult(
                r.Id, r.Kind, r.ChannelAlias, r.Text, r.Status, r.CreatedAt, r.SentAt, r.DeletedAt))
            .ToList();

        return new PagedResult<HistoryItemResult>(items, query.Page, query.PageSize, total);
    }
}
