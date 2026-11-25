using TelegramApi.Application.Common;

namespace TelegramApi.Application.TelegramInfo.CheckBot;

public record CheckBotQuery;

public class CheckBotHandler
{
    public static Task Handle(CheckBotQuery query, ITelegramService telegram, CancellationToken ct) =>
        telegram.CheckBotAsync(ct);
}
