using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.DeletePublication;

public record DeletePublicationCommand(Guid Id);

public class DeletePublicationHandler
{
    public static async Task Handle(
        DeletePublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<DeletePublicationHandler> logger,
        CancellationToken ct)
    {
        var publication = await db.Publications
            .Include(p => p.MediaFiles)
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Publication {command.Id} was not found.");

        var messageIds = publication.MediaFiles
            .Select(m => m.TgMessageId)
            .Append(publication.TgMsgId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        await DeletionFlow.ExecuteAsync(publication, messageIds, db, telegram, logger, ct);
    }
}
