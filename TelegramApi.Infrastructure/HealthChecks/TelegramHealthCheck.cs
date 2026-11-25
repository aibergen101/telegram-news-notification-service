using Microsoft.Extensions.Diagnostics.HealthChecks;
using TelegramApi.Application.Common;

namespace TelegramApi.Infrastructure.HealthChecks;

/// <summary>
/// Отдельная проверка Telegram (ТЗ 11.2, 11.3): доступность Telegram API, корректность токена
/// и возможность обращения от имени бота. В основной /health не входит.
/// </summary>
public sealed class TelegramHealthCheck(ITelegramService telegram) : IHealthCheck
{
    public const string Tag = "telegram";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await telegram.CheckBotAsync(cancellationToken);
            return HealthCheckResult.Healthy("Telegram Bot API is reachable and the bot token is valid.");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Telegram Bot API check failed.");
        }
    }
}
