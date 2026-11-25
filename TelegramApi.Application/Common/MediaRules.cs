using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

/// <summary>Какие файлы принимаются и как они отправляются в Telegram.</summary>
public static class MediaRules
{
    private static readonly HashSet<string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    private static readonly HashSet<string> DocumentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif",
        "image/webp",
        "application/pdf",
        "application/zip",
        "application/json",
        "text/plain",
        "text/csv",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation"
    };

    public static bool IsAllowed(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && (PhotoTypes.Contains(contentType) || DocumentTypes.Contains(contentType));

    public static MediaType Classify(string? contentType) =>
        contentType is not null && PhotoTypes.Contains(contentType) ? MediaType.Photo : MediaType.Document;
}
