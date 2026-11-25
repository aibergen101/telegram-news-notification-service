using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateText;

public record CreateTextPublicationCommand(
    string ChannelAlias,
    string Text,
    TextFormat Format,
    IReadOnlyList<ButtonDto> Buttons,
    string? IdempotencyKey = null);

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

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey();
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
        var alias = ChannelAlias.Normalize(command.ChannelAlias);
        var requestHash = command.IdempotencyKey is null ? null : ComputeRequestHash(command, alias);
        var lookup = db.Publications.Include(p => p.Buttons).Include(p => p.MediaFiles);

        var existing = await Idempotency.FindExistingAsync(lookup, command.IdempotencyKey, requestHash, ct);
        if (existing is not null)
        {
            return new CreateResult<PublicationDetails>(existing.ToDetails(), IsReplay: true);
        }

        await telegram.EnsureCanPublishAsync(alias, ct);

        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            Text = command.Text,
            Format = command.Format,
            CreatedAt = DateTime.UtcNow,
            Buttons = command.Buttons.ToEntities(),
            IdempotencyKey = command.IdempotencyKey,
            RequestHash = requestHash
        };

        var concurrent = await Idempotency.SaveNewAsync(db, db.Publications, lookup, publication, ct);
        if (concurrent is not null)
        {
            return new CreateResult<PublicationDetails>(concurrent.ToDetails(), IsReplay: true);
        }

        await PublishFlow.ExecuteAsync(publication, db, logger, async () =>
        {
            var sent = await telegram.SendTextAsync(alias, command.Text, command.Format, command.Buttons, ct);
            publication.MarkSent(sent.ChatId, sent.MessageId, DateTime.UtcNow);
        });

        return new CreateResult<PublicationDetails>(publication.ToDetails(), IsReplay: false);
    }

    private static string ComputeRequestHash(CreateTextPublicationCommand command, string alias) =>
        Idempotency.ComputeHash(new
        {
            Kind = "text",
            ChannelAlias = alias,
            command.Text,
            command.Format,
            command.Buttons
        });
}
