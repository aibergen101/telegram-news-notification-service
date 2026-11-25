using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.EditPublication;

/// <summary>
/// Полная замена редактируемого содержимого опубликованного сообщения.
/// Для текстовой публикации это текст, форматирование и кнопки; для медиа-публикации только подпись и форматирование.
/// </summary>
public record EditPublicationCommand(
    Guid Id,
    string? Text,
    TextFormat Format,
    IReadOnlyList<ButtonDto> Buttons);

public class EditPublicationValidator : AbstractValidator<EditPublicationCommand>
{
    public EditPublicationValidator()
    {
        RuleFor(x => x.Text)
            .MaximumLength(TelegramLimits.MaxMessageLength);

        RuleFor(x => x.Format).IsInEnum();

        RuleFor(x => x.Buttons)
            .Must(buttons => buttons.Count <= TelegramLimits.MaxButtons)
            .WithMessage($"No more than {TelegramLimits.MaxButtons} buttons are allowed.");

        RuleForEach(x => x.Buttons).SetValidator(new ButtonDtoValidator());
    }
}

public class EditPublicationHandler
{
    public static async Task<PublicationDetails> Handle(
        EditPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<EditPublicationHandler> logger,
        CancellationToken ct)
    {
        var publication = await db.Publications
            .Include(p => p.Buttons)
            .Include(p => p.MediaFiles)
            .FirstOrDefaultAsync(p => p.Id == command.Id, ct)
            ?? throw new NotFoundException($"Publication {command.Id} was not found.");

        if (publication.Status != PublicationStatus.Sent)
        {
            throw new ConflictException(
                $"Publication {publication.Id} has status {publication.Status}: only sent publications can be edited.");
        }

        // Правила зависят от вида сообщения в Telegram, поэтому проверяются после загрузки записи.
        EnsureEditAllowed(publication, command);

        if (publication.HasMedia)
        {
            // У альбома подпись хранится в первом сообщении, его идентификатор и записан в TgMsgId.
            await telegram.EditCaptionAsync(
                publication.TgChatId, publication.TgMsgId, command.Text, command.Format, ct);
        }
        else
        {
            await telegram.EditTextAsync(
                publication.TgChatId, publication.TgMsgId, command.Text!, command.Format, command.Buttons, ct);
        }

        publication.ApplyEdit(command.Text, command.Format, command.Buttons.ToEntities(), DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Publication {PublicationId} in {Channel} edited", publication.Id, publication.TgAlias);

        return publication.ToDetails();
    }

    private static void EnsureEditAllowed(Publication publication, EditPublicationCommand command)
    {
        var errors = new List<ValidationFailure>();

        if (publication.HasMedia)
        {
            if (command.Text is { Length: > TelegramLimits.MaxCaptionLength })
            {
                errors.Add(new ValidationFailure(
                    nameof(command.Text),
                    $"Caption must not be longer than {TelegramLimits.MaxCaptionLength} characters."));
            }

            if (command.Buttons.Count > 0)
            {
                errors.Add(new ValidationFailure(
                    nameof(command.Buttons),
                    "Buttons can be set only on text publications: media publications are created without buttons."));
            }
        }
        else if (string.IsNullOrWhiteSpace(command.Text))
        {
            errors.Add(new ValidationFailure(nameof(command.Text), "Text of a text publication must not be empty."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
