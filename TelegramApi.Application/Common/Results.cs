using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public record ButtonResult(string Text, string Url);

public record MediaFileResult(string FileName, MediaType MediaType, string TgFileId, int TgMessageId, int Order);

public record PublicationDetails(
    Guid Id,
    string ChannelAlias,
    long TgChatId,
    int TgMessageId,
    PublicationStatus Status,
    string? Text,
    TextFormat Format,
    IReadOnlyList<ButtonResult> Buttons,
    IReadOnlyList<MediaFileResult> MediaFiles,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? EditedAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    int? ErrorCode,
    string? ErrorText,
    DateTime? ErrorDate);

public record PollOptionResult(string Text, int Order);

public record PollDetails(
    Guid Id,
    string ChannelAlias,
    long TgChatId,
    int TgMessageId,
    PublicationStatus Status,
    string Question,
    IReadOnlyList<PollOptionResult> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    int? Duration,
    bool IsClosed,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ClosedAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    int? ErrorCode,
    string? ErrorText,
    DateTime? ErrorDate);

public record HistoryItemResult(
    Guid Id,
    PublicationKind Kind,
    string ChannelAlias,
    string? Text,
    PublicationStatus Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeletedAt);

/// <summary>Результат создания: новая запись или уже существующая по тому же ключу идемпотентности.</summary>
public record CreateResult<T>(T Details, bool IsReplay);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
