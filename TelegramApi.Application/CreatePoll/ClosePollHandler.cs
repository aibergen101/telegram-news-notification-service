using TelegramApi.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace TelegramApi.Application.Publications.CreatePoll;

public class ClosePollHandler
{
    public static async Task Handle(ClosePollCommand cmd, IApplicationDbContext db, ITelegramService tg)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == cmd.PollId)
            ?? throw new InvalidOperationException($"Poll {cmd.PollId} not found");

        if (!poll.IsClosed)
        {
            await tg.StopPollAsync(poll.TgChatId, poll.TgMsgId);
            poll.IsClosed = true;
            await db.SaveChangesAsync();
        }
    }
}