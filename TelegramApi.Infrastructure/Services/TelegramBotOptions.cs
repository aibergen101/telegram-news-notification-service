using Telegram.Bot;

namespace TelegramApi.Infrastructure;
public class TelegramBotOptions
{
    public string BotToken { get; set; } = string.Empty;
    public const string SectionName = "TelegramBot";
}