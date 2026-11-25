using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.EditPublication;

/// <summary>
/// Редактирование отправленной публикации (ТЗ 15.1): текст или подпись, формат и кнопки заменяются целиком.
/// Файлы не меняются. Опросы Telegram редактировать не позволяет.
/// </summary>
/// <param name="Text">Новый текст; для публикации с файлами это подпись, её можно убрать (null).</param>
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
                $"Publication {publication.Id} has status {publication.Status} and cannot be edited.");
        }

        // Правила, зависящие от вида уже отправленного сообщения.
        ValidateAgainstMessageKind(publication, command);

        if (publication.HasMedia)
        {
            // Подпись альбома хранится в первом сообщении, оно же TgMsgId публикации.
            await telegram.EditCaptionAsync(
                publication.TgChatId, publication.TgMsgId, command.Text, command.Format, command.Buttons, ct);
        }
        else
        {
            await telegram.EditTextAsync(
                publication.TgChatId, publication.TgMsgId, command.Text!, command.Format, command.Buttons, ct);
        }

        publication.MarkEdited(command.Text, command.Format, command.Buttons.ToEntities(), DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Publication {PublicationId} in {Channel} edited", publication.Id, publication.TgAlias);

        return publication.ToDetails();
    }

    private static void ValidateAgainstMessageKind(Publication publication, EditPublicationCommand command)
    {
        var errors = new List<ValidationFailure>();

        if (!publication.HasMedia && string.IsNullOrEmpty(command.Text))
        {
            errors.Add(new(nameof(command.Text), "Text of a text publication must not be empty."));
        }

        if (publication.HasMedia && command.Text?.Length > TelegramLimits.MaxCaptionLength)
        {
            errors.Add(new(
                nameof(command.Text),
                $"Caption must not be longer than {TelegramLimits.MaxCaptionLength} characters."));
        }

        if (publication.IsAlbum && command.Buttons.Count > 0)
        {
            errors.Add(new(
                nameof(command.Buttons),
                "Buttons can be attached only to a single file: Telegram does not attach them to albums."));
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}
