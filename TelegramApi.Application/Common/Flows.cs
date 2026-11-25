using Microsoft.Extensions.Logging;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public static class PublishFlow
{
    public static async Task ExecuteAsync(
        PublishableEntity entity,
        IApplicationDbContext db,
        ILogger logger,
        Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (TelegramOperationException ex)
        {
            entity.MarkFailed(ex.ErrorCode, ex.Message, DateTime.UtcNow);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning(
                "Publication {PublicationId} to {Channel} failed. Telegram error code: {ErrorCode}",
                entity.Id, entity.TgAlias, ex.ErrorCode);
            throw;
        }
        catch (OperationCanceledException)
        {
            entity.MarkFailed(null, "The operation was cancelled before Telegram confirmed it.", DateTime.UtcNow);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Publication {PublicationId} to {Channel} was cancelled", entity.Id, entity.TgAlias);
            throw;
        }
        catch (Exception ex)
        {
            entity.MarkFailed(null, "Unexpected error while publishing.", DateTime.UtcNow);
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogError(ex, "Publication {PublicationId} to {Channel} failed unexpectedly", entity.Id, entity.TgAlias);
            throw;
        }

        await db.SaveChangesAsync(CancellationToken.None);
        logger.LogInformation("Publication {PublicationId} sent to {Channel}", entity.Id, entity.TgAlias);
    }
}

public static class DeletionFlow
{
    public static async Task ExecuteAsync(
        PublishableEntity entity,
        IReadOnlyCollection<int> messageIds,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger logger,
        CancellationToken ct)
    {
        if (entity.Status == PublicationStatus.Deleted)
        {
            return;
        }

        if (entity.Status == PublicationStatus.Sent && messageIds.Count > 0)
        {
            await telegram.DeleteMessagesAsync(entity.TgChatId, messageIds, ct);
        }

        entity.MarkDeleted(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Publication {PublicationId} in {Channel} deleted", entity.Id, entity.TgAlias);
    }
}
