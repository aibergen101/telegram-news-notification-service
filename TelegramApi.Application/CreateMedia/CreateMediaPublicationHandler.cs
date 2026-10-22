using TelegramApi.Domain;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreateMedia;

public class CreateMediaPublicationHandler
{
    public static async Task<Guid> Handle(CreateMediaPublicationCommand cmd, IApplicationDbContext db, ITelegramService tg)
    {
        if (!await tg.CanPublishToChannelAsync(cmd.ChannelAlias))
            throw new InvalidOperationException($"Channel '{cmd.ChannelAlias}' is not accessible or bot lacks required permissions");

        Publication publication = new Publication
        {
            Id = Guid.NewGuid(),
            Text = cmd.Text,
            TgAlias = cmd.ChannelAlias,
            TgMsgId = 0,
            TgChatId = 0,
            Status = PublicationStatus.Failed,
            CreatedAt = DateTime.UtcNow
        };

        publication.Buttons = cmd.Buttons?.Select(x => new Button
        {
            Id = Guid.NewGuid(),
            Text = x.Text,
            Url = x.Url,
            PublicationId = publication.Id
        }).ToList() ?? new();

        db.Publications.Add(publication);
        await db.SaveChangesAsync();

        try
        {
            var files = await tg.SendMediaAsync(cmd.ChannelAlias, cmd.Text, cmd.Buttons, cmd.MediaFiles);

            publication.MediaFiles = cmd.MediaFiles.Zip(files, (file, result) => new MediaFile
            {
                Id = Guid.NewGuid(),
                FileName = file.FileName,
                MediaType = result.MediaType,
                TgFileId = result.TgFileId,
                PublicationId = publication.Id
            }).ToList();

            publication.TgMsgId = files.First().MessageId;
            publication.TgChatId = files.First().ChatId;
            publication.Status = PublicationStatus.Sent;
            publication.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            publication.Status = PublicationStatus.Failed;
            publication.ErrorText = ex.Message;
            publication.ErrorDate = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return publication.Id;
    }
}