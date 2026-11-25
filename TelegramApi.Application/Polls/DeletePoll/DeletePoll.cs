using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Polls.DeletePoll;

public record DeletePollCommand(Guid Id);

public class DeletePollHandler
{
    public static async Task Handle(
        DeletePollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<DeletePollHandler> logger,
        CancellationToken ct)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Poll {command.Id} was not found.");

        await DeletionFlow.ExecuteAsync(poll, [poll.TgMsgId], db, telegram, logger, ct);
    }
}
