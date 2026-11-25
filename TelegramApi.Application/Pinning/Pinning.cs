using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Pinning;

/// <summary>Закрепить сообщение публикации или опроса в канале (ТЗ 15.2).</summary>
/// <param name="DisableNotification">Закрепить без уведомления подписчиков.</param>
public record PinMessageCommand(PublicationKind Kind, Guid Id, bool DisableNotification = false);

/// <summary>Открепить сообщение публикации или опроса (ТЗ 15.2).</summary>
public record UnpinMessageCommand(PublicationKind Kind, Guid Id);

public class PinMessageHandler
{
    public static async Task Handle(
        PinMessageCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<PinMessageHandler> logger,
        CancellationToken ct)
    {
        var entity = await PinTarget.LoadSentAsync(db, command.Kind, command.Id, ct);
        if (entity.IsPinned)
        {
            return;
        }

        await telegram.PinMessageAsync(entity.TgChatId, entity.TgMsgId, command.DisableNotification, ct);

        entity.MarkPinned(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Kind} {Id} pinned in {Channel}", command.Kind, entity.Id, entity.TgAlias);
    }
}

public class UnpinMessageHandler
{
    public static async Task Handle(
        UnpinMessageCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<UnpinMessageHandler> logger,
        CancellationToken ct)
    {
        var entity = await PinTarget.LoadSentAsync(db, command.Kind, command.Id, ct);
        if (!entity.IsPinned)
        {
            return;
        }

        await telegram.UnpinMessageAsync(entity.TgChatId, entity.TgMsgId, ct);

        entity.MarkUnpinned(DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("{Kind} {Id} unpinned in {Channel}", command.Kind, entity.Id, entity.TgAlias);
    }
}

internal static class PinTarget
{
    /// <summary>Закрепление имеет смысл только для сообщения, которое есть в Telegram (статус Sent).</summary>
    public static async Task<PublishableEntity> LoadSentAsync(
        IApplicationDbContext db,
        PublicationKind kind,
        Guid id,
        CancellationToken ct)
    {
        PublishableEntity? entity = kind == PublicationKind.Poll
            ? await db.Polls.FirstOrDefaultAsync(p => p.Id == id, ct)
            : await db.Publications.FirstOrDefaultAsync(p => p.Id == id, ct);

        var name = kind == PublicationKind.Poll ? "Poll" : "Publication";

        if (entity is null)
        {
            throw new NotFoundException($"{name} {id} was not found.");
        }

        if (entity.Status != PublicationStatus.Sent)
        {
            throw new ConflictException($"{name} {id} has status {entity.Status} and cannot be pinned or unpinned.");
        }

        return entity;
    }
}
