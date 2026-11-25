using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateMedia;

public record CreateMediaPublicationCommand(
    string ChannelAlias,
    string? Text,
    TextFormat Format,
    IReadOnlyList<MediaFileDto> Files);

public class CreateMediaPublicationValidator : AbstractValidator<CreateMediaPublicationCommand>
{
    public CreateMediaPublicationValidator()
    {
        RuleFor(x => x.ChannelAlias).MustBeChannelAlias();

        RuleFor(x => x.Text)
            .MaximumLength(TelegramLimits.MaxCaptionLength)
            .WithMessage($"Caption must not be longer than {TelegramLimits.MaxCaptionLength} characters.");

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
    public static async Task<PublicationDetails> Handle(
        CreateMediaPublicationCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateMediaPublicationHandler> logger,
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
            CreatedAt = DateTime.UtcNow
        };

        db.Publications.Add(publication);
        await db.SaveChangesAsync(ct);

        await PublishFlow.ExecuteAsync(publication, db, logger, async () =>
        {
            var results = await telegram.SendMediaAsync(
                alias, command.Text, command.Format, command.Files, ct);

            for (var i = 0; i < results.Count; i++)
            {
                publication.MediaFiles.Add(new MediaFile
                {
                    FileName = command.Files[i].FileName,
                    MediaType = results[i].MediaType,
                    TgFileId = results[i].TgFileId,
                    TgMessageId = results[i].MessageId,
                    Order = i
                });
            }

            publication.MarkSent(results[0].ChatId, results[0].MessageId, DateTime.UtcNow);
        });

        return publication.ToDetails();
    }
}
