using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.DeletePublication;

public class DeletePublicationHandler
{
    public static async Task Handle(DeletePublicationCommand cmd, IApplicationDbContext db, ITelegramService tg)
    {
        var publication = await db.Publications.FirstOrDefaultAsync(p => p.Id == cmd.Id)
            ?? throw new InvalidOperationException($"Publication {cmd.Id} not found");

        if (publication.Status == PublicationStatus.Deleted)
            return; 

        if (publication.Status == PublicationStatus.Sent)
        {
            await tg.DeleteMessageAsync(publication.TgChatId, publication.TgMsgId);
        }

        publication.Status = PublicationStatus.Deleted;
        publication.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}