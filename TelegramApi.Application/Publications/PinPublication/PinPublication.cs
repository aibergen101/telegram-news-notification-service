using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.PinPublication;

/// <param name="DisableNotification">Закрепить без уведомления подписчиков канала.</param>
public record PinPublicationCommand(Guid Id, bool DisableNotification);

public class PinPublicationHandler
{
    public static async Task Handle(
        PinPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<PinPublicationHandler> logger,
        CancellationToken ct)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Publication {command.Id} was not found.");

        await PinFlow.ExecuteAsync(publication, pin: true, command.DisableNotification, db, telegram, logger, ct);
    }
}
