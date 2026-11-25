using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.UnpinPublication;

public record UnpinPublicationCommand(Guid Id);

public class UnpinPublicationHandler
{
    public static async Task Handle(
        UnpinPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<UnpinPublicationHandler> logger,
        CancellationToken ct)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Publication {command.Id} was not found.");

        await PinFlow.ExecuteAsync(publication, pin: false, disableNotification: false, db, telegram, logger, ct);
    }
}
