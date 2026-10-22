namespace TelegramApi.Domain;

public class Publication
{
    public Guid Id { get; set; }
    public int TgMsgId { get; set; }
    public long TgChatId { get; set; }
    public string TgAlias { get; set; }
    public PublicationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string Text { get; set; }
    public int? ErrorCode { get; set; }
    public string? ErrorText { get; set; }
    public DateTime? ErrorDate { get; set; }
    public List<Button> Buttons { get; set; } = new();
    public List<MediaFile> MediaFiles { get; set; } = new();
    public PublicationType PublicationType { get; set; }
}
