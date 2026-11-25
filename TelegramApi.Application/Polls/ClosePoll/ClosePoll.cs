using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Polls.ClosePoll;

public record ClosePollCommand(Guid Id);

public class ClosePollHandler
{
    public static async Task Handle(
        ClosePollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<ClosePollHandler> logger,
        CancellationToken ct)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Poll {command.Id} was not found.");

        if (poll.Status != PublicationStatus.Sent)
        {
            throw new ConflictException($"Poll {poll.Id} has status {poll.Status} and cannot be closed.");
        }

        if (poll.IsClosed)
        {
            return;
        }

        await telegram.StopPollAsync(poll.TgChatId, poll.TgMsgId, ct);

        poll.MarkClosed(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Poll {PollId} in {Channel} closed", poll.Id, poll.TgAlias);
    }
}
