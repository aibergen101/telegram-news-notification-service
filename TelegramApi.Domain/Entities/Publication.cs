namespace TelegramApi.Domain;

/// <summary>Обычная публикация: текст, текст с кнопками или медиа с подписью.</summary>
public class Publication : PublishableEntity
{
    /// <summary>Текст сообщения или подпись к медиа. У медиа-публикации может отсутствовать.</summary>
    public string? Text { get; set; }

    /// <summary>Ссылка, переданная отдельным полем запроса (ТЗ 2.8); добавляется в конец текста сообщения.</summary>
    public string? Link { get; set; }

    public TextFormat Format { get; set; } = TextFormat.Plain;

    public List<Button> Buttons { get; set; } = [];

    public List<MediaFile> MediaFiles { get; set; } = [];

    /// <summary>Дата последнего редактирования сообщения в Telegram.</summary>
    public DateTime? EditedAt { get; set; }

    public bool HasMedia => MediaFiles.Count > 0;

    /// <summary>Альбом: несколько файлов в одной media group.</summary>
    public bool IsAlbum => MediaFiles.Count > 1;

    /// <summary>Фиксирует новое содержимое после успешного редактирования в Telegram.</summary>
    public void MarkEdited(string? text, string? link, TextFormat format, List<Button> buttons, DateTime now)
    {
        Text = text;
        Link = link;
        Format = format;
        Buttons.Clear();
        Buttons.AddRange(buttons);
        EditedAt = now;
        UpdatedAt = now;
    }
}
