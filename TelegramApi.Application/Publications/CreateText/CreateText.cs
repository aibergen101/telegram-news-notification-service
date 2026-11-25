using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateText;

public record CreateTextPublicationCommand(
    string ChannelAlias,
    string Text,
    TextFormat Format,
    IReadOnlyList<ButtonDto> Buttons);

public class CreateTextPublicationValidator : AbstractValidator<CreateTextPublicationCommand>
{
    public CreateTextPublicationValidator()
    {
        RuleFor(x => x.ChannelAlias).MustBeChannelAlias();

        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(TelegramLimits.MaxMessageLength);

        RuleFor(x => x.Format).IsInEnum();

        RuleFor(x => x.Buttons)
            .Must(buttons => buttons.Count <= TelegramLimits.MaxButtons)
            .WithMessage($"No more than {TelegramLimits.MaxButtons} buttons are allowed.");

        RuleForEach(x => x.Buttons).SetValidator(new ButtonDtoValidator());
    }
}

public class CreateTextPublicationHandler
{
    public static async Task<PublicationDetails> Handle(
        CreateTextPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateTextPublicationHandler> logger,
        CancellationToken ct)
    {
        var alias = ChannelAlias.Normalize(command.ChannelAlias);
        await telegram.EnsureCanPublishAsync(alias, ct);

        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            Text = command.Text,
            Format = command.Format,
            CreatedAt = DateTime.UtcNow,
            Buttons = command.Buttons.ToEntities()
        };

        db.Publications.Add(publication);
        await db.SaveChangesAsync(ct);

        await PublishFlow.ExecuteAsync(publication, db, logger, async () =>
        {
            var sent = await telegram.SendTextAsync(alias, command.Text, command.Format, command.Buttons, ct);
            publication.MarkSent(sent.ChatId, sent.MessageId, DateTime.UtcNow);
        });

        return publication.ToDetails();
    }
}
