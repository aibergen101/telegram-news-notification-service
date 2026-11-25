namespace TelegramApi.Domain;

public class Poll : PublishableEntity
{
    public string Question { get; set; } = string.Empty;

    public bool AllowMultipleAnswers { get; set; }

    public bool IsAnonymous { get; set; } = true;

    public int? Duration { get; set; }

    public bool IsClosed { get; set; }

    public DateTime? ClosedAt { get; set; }

    public List<PollOption> Options { get; set; } = [];

    public void MarkClosed(DateTime now)
    {
        IsClosed = true;
        ClosedAt = now;
        UpdatedAt = now;
    }
}
