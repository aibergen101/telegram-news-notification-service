using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Polls.GetPoll;

public record GetPollQuery(Guid Id);

public class GetPollHandler
{
    public static async Task<PollDetails> Handle(GetPollQuery query, IApplicationDbContext db, CancellationToken ct)
    {
        var poll = await db.Polls
            .AsNoTracking()
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Id == query.Id, ct)
            ?? throw new NotFoundException($"Poll {query.Id} was not found.");

        return poll.ToDetails();
    }
}
