namespace TelegramApi.Domain;

public class PollOption
{
    public Guid Id { get; set; }
    public string Text { get; set; }
    public int Order { get; set; }
    public Guid PollId { get; set; }
    public Poll Poll { get; set; }
}
