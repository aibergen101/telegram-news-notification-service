namespace TelegramApi.Domain;

public enum MediaType
{
    Photo,
    Document
}

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

public enum TextFormat
{
    Plain,
    Html,
    MarkdownV2
}
