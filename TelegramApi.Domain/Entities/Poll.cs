namespace TelegramApi.Domain;

/// <summary>Опрос в канале.</summary>
public class Poll : PublishableEntity
{
    public string Question { get; set; } = string.Empty;

    public bool AllowMultipleAnswers { get; set; }

    public bool IsAnonymous { get; set; } = true;

    /// <summary>Продолжительность опроса в секундах; null, если опрос не ограничен по времени.</summary>
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
