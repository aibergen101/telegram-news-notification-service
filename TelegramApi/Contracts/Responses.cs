using System.ComponentModel;
using TelegramApi.Domain;

namespace TelegramApi.Contracts;

public record ButtonResponse(string Text, string Url);

public record MediaFileResponse(string FileName, MediaType MediaType, string TgFileId, int TgMessageId, int Order);

[Description("Публикация со всеми сохранёнными данными.")]
public record PublicationResponse(
    Guid Id,
    string ChannelAlias,
    long TgChatId,
    int TgMessageId,
    PublicationStatus Status,
    string? Text,
    TextFormat Format,
    IReadOnlyList<ButtonResponse> Buttons,
    IReadOnlyList<MediaFileResponse> MediaFiles,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    [property: Description("Когда сообщение последний раз редактировалось.")] DateTime? EditedAt,
    [property: Description("Сообщение закреплено в канале.")] bool IsPinned,
    DateTime? PinnedAt,
    [property: Description("Код ошибки Telegram, если публикация не удалась.")] int? ErrorCode,
    string? ErrorText,
    DateTime? ErrorDate);

public record PollOptionResponse(string Text, int Order);

[Description("Опрос со всеми сохранёнными данными.")]
public record PollResponse(
    Guid Id,
    string ChannelAlias,
    long TgChatId,
    int TgMessageId,
    PublicationStatus Status,
    string Question,
    IReadOnlyList<PollOptionResponse> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    [property: Description("Продолжительность в секундах.")] int? Duration,
    bool IsClosed,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? ClosedAt,
    DateTime? DeletedAt,
    DateTime? UpdatedAt,
    [property: Description("Сообщение закреплено в канале.")] bool IsPinned,
    DateTime? PinnedAt,
    int? ErrorCode,
    string? ErrorText,
    DateTime? ErrorDate);

[Description("Запись единой истории: публикация или опрос.")]
public record HistoryItemResponse(
    Guid Id,
    [property: Description("Post — обычная публикация, Poll — опрос.")] PublicationKind Type,
    string ChannelAlias,
    [property: Description("Текст публикации или вопрос опроса.")] string? Text,
    PublicationStatus Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeletedAt);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record BotCheckResponse(string Status);

public record ChannelInfoResponse(
    long ChatId,
    string? Title,
    string? Username,
    string Type);
