namespace TelegramApi.Domain;

public class Button
{
    public Guid Id { get; set; }
    public string Text { get; set; }
    public string Url { get; set; }
    public Guid PublicationId { get; set; }
    public Publication Publication { get; set; }
}
