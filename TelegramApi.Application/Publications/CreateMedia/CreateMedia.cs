using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateMedia;

/// <param name="Buttons">Кнопки со ссылками; Telegram прикрепляет их только к одиночному файлу, не к альбому.</param>
/// <param name="IdempotencyKey">Необязательный ключ идемпотентности из заголовка Idempotency-Key.</param>
public record CreateMediaPublicationCommand(
    string ChannelAlias,
    string? Text,
    TextFormat Format,
    IReadOnlyList<MediaFileDto> Files,
    IReadOnlyList<ButtonDto> Buttons,
    string? IdempotencyKey = null,
    string? Link = null);

public class CreateMediaPublicationValidator : AbstractValidator<CreateMediaPublicationCommand>
{
    public CreateMediaPublicationValidator()
    {
        RuleFor(x => x.ChannelAlias).MustBeChannelAlias();

        RuleFor(x => x.Link).MustBeLink();
        RuleFor(x => x)
            .Must(x => MessageText.Length(x.Text, x.Link) <= TelegramLimits.MaxCaptionLength)
            .OverridePropertyName(nameof(CreateMediaPublicationCommand.Text))
            .WithMessage($"Caption together with the link must not be longer than {TelegramLimits.MaxCaptionLength} characters.");

        RuleFor(x => x.Format).IsInEnum();

        RuleFor(x => x.Files)
            .NotEmpty()
            .WithMessage("At least one file is required.");

        RuleFor(x => x.Files)
            .Must(files => files.Count <= TelegramLimits.MaxAlbumItems)
            .WithMessage($"No more than {TelegramLimits.MaxAlbumItems} files are allowed in one publication.");

        RuleFor(x => x.Files)
            .Must(files => files.Select(f => MediaRules.Classify(f.ContentType)).Distinct().Count() <= 1)
            .WithMessage("Images and documents cannot be mixed in one publication: Telegram does not allow such albums.");

        RuleForEach(x => x.Files).SetValidator(new MediaFileDtoValidator());

        RuleFor(x => x.Buttons)
            .Must(buttons => buttons.Count <= TelegramLimits.MaxButtons)
            .WithMessage($"No more than {TelegramLimits.MaxButtons} buttons are allowed.");

        RuleFor(x => x.Buttons)
            .Empty()
            .When(x => x.Files.Count > 1)
            .WithMessage("Buttons can be attached only to a single file: Telegram does not attach them to albums.");

        RuleForEach(x => x.Buttons).SetValidator(new ButtonDtoValidator());

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey().When(x => x.IdempotencyKey is not null);
    }
}

public class MediaFileDtoValidator : AbstractValidator<MediaFileDto>
{
    public MediaFileDtoValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(TelegramLimits.MaxFileNameLength);

        RuleFor(x => x.ContentType)
            .Must(MediaRules.IsAllowed)
            .WithMessage(file => $"Content type '{file.ContentType}' is not allowed.");

        RuleFor(x => x.Length)
            .GreaterThan(0)
            .WithMessage("File must not be empty.");

        RuleFor(x => x.Length)
            .LessThanOrEqualTo(TelegramLimits.MaxPhotoBytes)
            .When(x => MediaRules.Classify(x.ContentType) == MediaType.Photo)
            .WithMessage("An image must not be larger than 10 MB.");

        RuleFor(x => x.Length)
            .LessThanOrEqualTo(TelegramLimits.MaxDocumentBytes)
            .When(x => MediaRules.Classify(x.ContentType) == MediaType.Document)
            .WithMessage("A document must not be larger than 50 MB.");
    }
}

public class CreateMediaPublicationHandler
{
    public static async Task<CreateResult<PublicationDetails>> Handle(
        CreateMediaPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateMediaPublicationHandler> logger,
        CancellationToken ct)
    {
        var alias = command.ChannelAlias;

        string? hash = null;
        if (command.IdempotencyKey is { } key)
        {
            var files = new List<object>(command.Files.Count);
            foreach (var file in command.Files)
            {
                files.Add(new
                {
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    Sha256 = await Idempotency.HashStreamAsync(file.Content, ct)
                });
            }

            hash = Idempotency.ComputeHash(new
            {
                Kind = "media",
                Alias = alias,
                command.Text,
                command.Link,
                command.Format,
                Files = files,
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
            // Файлы записываются до отправки, чтобы и у Failed-публикации было видно, что в ней было (ТЗ 4.7).
            MediaFiles = command.Files
                .Select((file, index) => new MediaFile
                {
                    FileName = file.FileName,
                    MediaType = MediaRules.Classify(file.ContentType),
                    Order = index
                })
                .ToList(),
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
            var caption = MessageText.Compose(command.Text, command.Link, command.Format);
            var results = await telegram.SendMediaAsync(
                alias, caption, command.Format, command.Files, command.Buttons, ct);

            var files = publication.MediaFiles.OrderBy(m => m.Order).ToList();
            for (var i = 0; i < results.Count; i++)
            {
                files[i].MediaType = results[i].MediaType;
                files[i].TgFileId = results[i].TgFileId;
                files[i].TgMessageId = results[i].MessageId;
            }

            publication.MarkSent(results[0].ChatId, results[0].MessageId, DateTime.UtcNow);
        });

        return new(publication.ToDetails(), Replayed: false);
    }
}
