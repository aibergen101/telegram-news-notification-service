namespace TelegramApi.Domain;

public class MediaFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public string? TgFileId { get; set; }

    public int? TgMessageId { get; set; }

    public int Order { get; set; }

    public Guid PublicationId { get; set; }

    public Publication Publication { get; set; } = null!;
}
