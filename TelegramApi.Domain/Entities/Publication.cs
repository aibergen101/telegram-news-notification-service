namespace TelegramApi.Domain;

public class Publication : PublishableEntity
{
    public string? Text { get; set; }

    public string? Link { get; set; }

    public TextFormat Format { get; set; } = TextFormat.Plain;

    public List<Button> Buttons { get; set; } = [];

    public List<MediaFile> MediaFiles { get; set; } = [];

    public DateTime? EditedAt { get; set; }

    public bool HasMedia => MediaFiles.Count > 0;

    public bool IsAlbum => MediaFiles.Count > 1;

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
