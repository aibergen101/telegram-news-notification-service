using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public record ButtonResult(string Text, string Url);

public record MediaFileResult(string FileName, MediaType MediaType, string? TgFileId, int? TgMessageId, int Order);

public record PublicationDetails(
    Guid Id,
    string ChannelAlias,
    long TgChatId,
    int TgMessageId,
    PublicationStatus Status,
    string? Text,
    string? Link,
    TextFormat Format,
    IReadOnlyList<ButtonResult> Buttons,
    IReadOnlyList<MediaFileResult> MediaFiles,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    DateTime? EditedAt,
    bool IsPinned,
    DateTime? PinnedAt,
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
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ClosedAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    bool IsPinned,
    DateTime? PinnedAt,
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

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
