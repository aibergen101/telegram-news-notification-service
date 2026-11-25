using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Polls.PinPoll;

/// <param name="DisableNotification">Закрепить без уведомления подписчиков канала.</param>
public record PinPollCommand(Guid Id, bool DisableNotification);

public class PinPollHandler
{
    public static async Task Handle(
        PinPollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<PinPollHandler> logger,
        CancellationToken ct)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Poll {command.Id} was not found.");

        await PinFlow.ExecuteAsync(poll, pin: true, command.DisableNotification, db, telegram, logger, ct);
    }
}
