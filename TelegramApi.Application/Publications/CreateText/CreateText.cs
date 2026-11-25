using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateText;

/// <param name="IdempotencyKey">Необязательный ключ идемпотентности из заголовка Idempotency-Key.</param>
public record CreateTextPublicationCommand(
    string ChannelAlias,
    string? Text,
    TextFormat Format,
    IReadOnlyList<ButtonDto> Buttons,
    string? IdempotencyKey = null,
    string? Link = null);

public class CreateTextPublicationValidator : AbstractValidator<CreateTextPublicationCommand>
{
    public CreateTextPublicationValidator()
    {
        RuleFor(x => x.ChannelAlias).MustBeChannelAlias();

        RuleFor(x => x.Text)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.Link))
            .WithMessage("Text or Link must be provided.");
        RuleFor(x => x.Link).MustBeLink();
        RuleFor(x => x)
            .Must(x => MessageText.Length(x.Text, x.Link) <= TelegramLimits.MaxMessageLength)
            .OverridePropertyName(nameof(CreateTextPublicationCommand.Text))
            .WithMessage($"Text together with the link must not be longer than {TelegramLimits.MaxMessageLength} characters.");

        RuleFor(x => x.Format).IsInEnum();

        RuleFor(x => x.Buttons)
            .Must(buttons => buttons.Count <= TelegramLimits.MaxButtons)
            .WithMessage($"No more than {TelegramLimits.MaxButtons} buttons are allowed.");

        RuleForEach(x => x.Buttons).SetValidator(new ButtonDtoValidator());

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey().When(x => x.IdempotencyKey is not null);
    }
}

public class CreateTextPublicationHandler
{
    public static async Task<CreateResult<PublicationDetails>> Handle(
        CreateTextPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateTextPublicationHandler> logger,
        CancellationToken ct)
    {
        var alias = command.ChannelAlias;

        string? hash = null;
        if (command.IdempotencyKey is { } key)
        {
            hash = Idempotency.ComputeHash(new
            {
                Kind = "text",
                Alias = alias,
                command.Text,
                command.Link,
                command.Format,
                command.Buttons
            });

            var replay = await Idempotency.FindReplayAsync<Publication>(db, key, hash, ct);
            if (replay is not null)
            {
                return new(replay.ToDetails(), Replayed: true);
            }
        }

        await telegram.EnsureCanPublishAsync(alias, ct);

        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            Text = command.Text,
            Link = command.Link,
            Format = command.Format,
            CreatedAt = DateTime.UtcNow,
            Buttons = command.Buttons.ToEntities(),
            IdempotencyKey = command.IdempotencyKey,
            IdempotencyHash = hash
        };

        db.Publications.Add(publication);
        if (await Idempotency.SaveNewAsync(publication, db, ct) is { } winner)
        {
            return new(winner.ToDetails(), Replayed: true);
        }

        await PublishFlow.ExecuteAsync(publication, db, logger, async () =>
        {
            var sent = await telegram.SendTextAsync(
                alias, MessageText.Compose(command.Text, command.Link, command.Format)!, command.Format, command.Buttons, ct);
            publication.MarkSent(sent.ChatId, sent.MessageId, DateTime.UtcNow);
        });

        return new(publication.ToDetails(), Replayed: false);
    }
}
