using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Polls.UnpinPoll;

public record UnpinPollCommand(Guid Id);

public class UnpinPollHandler
{
    public static async Task Handle(
        UnpinPollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<UnpinPollHandler> logger,
        CancellationToken ct)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Poll {command.Id} was not found.");

        await PinFlow.ExecuteAsync(poll, pin: false, disableNotification: false, db, telegram, logger, ct);
    }
}
