using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.GetPublication;

public record GetPublicationQuery(Guid Id);

public class GetPublicationHandler
{
    public static async Task<PublicationDetails> Handle(
        GetPublicationQuery query,
        IApplicationDbContext db,
        CancellationToken ct)
    {
        var publication = await db.Publications
            .AsNoTracking()
            .Include(p => p.Buttons)
            .Include(p => p.MediaFiles)
            .FirstOrDefaultAsync(p => p.Id == query.Id, ct)
            ?? throw new NotFoundException($"Publication {query.Id} was not found.");

        return publication.ToDetails();
    }
}
