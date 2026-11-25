namespace TelegramApi.Infrastructure;

public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    public string BotToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
