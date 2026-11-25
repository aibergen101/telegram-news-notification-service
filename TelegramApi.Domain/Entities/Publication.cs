namespace TelegramApi.Domain;

/// <summary>Обычная публикация: текст, текст с кнопками или медиа с подписью.</summary>
public class Publication : PublishableEntity
{
    /// <summary>Текст сообщения или подпись к медиа. У медиа-публикации может отсутствовать.</summary>
    public string? Text { get; set; }

    public TextFormat Format { get; set; } = TextFormat.Plain;

    public List<Button> Buttons { get; set; } = [];

    public List<MediaFile> MediaFiles { get; set; } = [];

    /// <summary>Когда сообщение в Telegram последний раз редактировалось через API.</summary>
    public DateTime? EditedAt { get; set; }

    /// <summary>Медиа-публикация: в Telegram у неё редактируется только подпись.</summary>
    public bool HasMedia => MediaFiles.Count > 0;

    public void ApplyEdit(string? text, TextFormat format, List<Button> buttons, DateTime now)
    {
        Text = text;
        Format = format;
        Buttons.Clear();
        Buttons.AddRange(buttons);
        EditedAt = now;
        UpdatedAt = now;
    }
}
