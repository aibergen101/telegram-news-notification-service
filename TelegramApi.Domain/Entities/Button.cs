namespace TelegramApi.Domain;

public class Button
{
    public Guid Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public int Order { get; set; }

    public Guid PublicationId { get; set; }

    public Publication Publication { get; set; } = null!;
}
