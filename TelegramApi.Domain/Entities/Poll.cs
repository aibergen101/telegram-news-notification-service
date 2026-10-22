namespace TelegramApi.Domain;

public class Poll
{
    public Guid Id { get; set; }
    public int TgMsgId{ get; set; }
    public long TgChatId { get; set; }
    public string Question { get; set; }
    public List<PollOption> Options { get; set; } = new();
    public int? Duration { get; set; }
    public bool IsClosed { get; set; }
    public bool AllowMultipleAnswers { get; set; }
    public bool IsAnonymous { get; set; }
    public string TgAlias { get; set; }
    public DateTime CreatedAt { get; set; }
    public PublicationStatus Status { get; set; } = PublicationStatus.Sent;
    public DateTime? DeletedAt { get; set; }
}
