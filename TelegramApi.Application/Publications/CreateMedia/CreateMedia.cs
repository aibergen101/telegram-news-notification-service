using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Publications.CreateMedia;

public record CreateMediaPublicationCommand(
    string ChannelAlias,
    string? Text,
    TextFormat Format,
    IReadOnlyList<MediaFileDto> Files,
    string? IdempotencyKey = null);

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

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey();
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
        var alias = ChannelAlias.Normalize(command.ChannelAlias);
        var requestHash = command.IdempotencyKey is null ? null : await ComputeRequestHashAsync(command, alias, ct);
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

        return new CreateResult<PublicationDetails>(publication.ToDetails(), IsReplay: false);
    }

    /// <summary>
    /// Файлы сравниваются по имени, типу и SHA-256 содержимого.
    /// После хеширования поток возвращается в начало, чтобы его можно было отправить в Telegram.
    /// </summary>
    private static async Task<string> ComputeRequestHashAsync(
        CreateMediaPublicationCommand command,
        string alias,
        CancellationToken ct)
    {
        var files = new List<object>(command.Files.Count);

        foreach (var file in command.Files)
        {
            string? contentHash = null;
            if (file.Content.CanSeek)
            {
                var start = file.Content.Position;
                contentHash = Convert.ToHexString(await SHA256.HashDataAsync(file.Content, ct));
                file.Content.Position = start;
            }

            files.Add(new { file.FileName, file.ContentType, file.Length, ContentHash = contentHash });
        }

        return Idempotency.ComputeHash(new
        {
            Kind = "media",
            ChannelAlias = alias,
            command.Text,
            command.Format,
            Files = files
        });
    }
}
