using System.ComponentModel.DataAnnotations;

namespace TelegramApi.Infrastructure;

/// <summary>Настройки бота. Токен берётся из user-secrets или переменных окружения, в репозитории его нет.</summary>
public sealed class TelegramBotOptions
{
    public const string SectionName = "TelegramBot";

    [Required(AllowEmptyStrings = false)]
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Максимальное время ожидания ответа Telegram, секунды.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
