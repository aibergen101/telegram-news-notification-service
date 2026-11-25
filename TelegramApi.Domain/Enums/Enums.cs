namespace TelegramApi.Domain;

public enum MediaType
{
    Photo,
    Document
}

/// <summary>Вид записи в единой истории публикаций.</summary>
public enum PublicationKind
{
    Post,
    Poll
}

public enum PublicationStatus
{
    Sent,
    Failed,
    Deleted
}

/// <summary>Режим форматирования текста или подписи (parse_mode в Telegram).</summary>
public enum TextFormat
{
    Plain,
    Html,
    MarkdownV2
}
