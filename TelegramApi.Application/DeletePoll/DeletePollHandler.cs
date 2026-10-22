using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreatePoll;

public class DeletePollHandler
{
    public static async Task Handle(DeletePollCommand cmd, IApplicationDbContext db, ITelegramService tg)
    {
        var poll = await db.Polls.FirstOrDefaultAsync(p => p.Id == cmd.Id)
            ?? throw new InvalidOperationException($"Poll {cmd.Id} not found");

        if (poll.Status == PublicationStatus.Deleted)
            return;

        if (poll.Status == PublicationStatus.Sent)
        {
            await tg.DeleteMessageAsync(poll.TgChatId, poll.TgMsgId);
        }

        poll.Status = PublicationStatus.Deleted;
        poll.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}