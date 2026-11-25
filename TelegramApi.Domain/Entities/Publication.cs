namespace TelegramApi.Domain;

/// <summary>Обычная публикация: текст, текст с кнопками или медиа с подписью.</summary>
public class Publication : PublishableEntity
{
    /// <summary>Текст сообщения или подпись к медиа. У медиа-публикации может отсутствовать.</summary>
    public string? Text { get; set; }

    public TextFormat Format { get; set; } = TextFormat.Plain;

    public List<Button> Buttons { get; set; } = [];

    public List<MediaFile> MediaFiles { get; set; } = [];
}
