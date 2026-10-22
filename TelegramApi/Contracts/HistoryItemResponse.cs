namespace TelegramApi.Contracts;

public record HistoryItemResponse(
    Guid Id,
    string Type, 
    string ChannelAlias,
    string? Text,
    string Status,
    DateTime CreatedAt,
    DateTime? SentAt,
    DateTime? DeletedAt);