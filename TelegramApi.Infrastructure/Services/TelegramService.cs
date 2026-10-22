using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Infrastructure;

public class TelegramService : ITelegramService
{
    private readonly ITelegramBotClient _botClient;

    public TelegramService(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task<(int MessageId, long ChatId)> SendTextMessageAsync(string channelAlias, string text, List<ButtonDto>? buttons, CancellationToken cancellationToken = default)
    {
        InlineKeyboardMarkup? replyMarkup = buttons == null || buttons.Count == 0
            ? null
            : new InlineKeyboardMarkup(buttons.Select(x => InlineKeyboardButton.WithUrl(x.Text, x.Url)));

        var message = await _botClient.SendMessage(channelAlias, text, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
        return (message.MessageId, message.Chat.Id);
    }
    public async Task<List<MediaSendResult>> SendMediaAsync(string channelAlias, string? text, List<ButtonDto>? buttons, List<MediaFileDto> mediaFiles, CancellationToken cancellationToken = default)
    {
        InlineKeyboardMarkup? replyMarkup = buttons == null || buttons.Count == 0
            ? null
            : new InlineKeyboardMarkup(buttons.Select(x => InlineKeyboardButton.WithUrl(x.Text, x.Url)));

        var mediaTypes = mediaFiles.Select(f => IsImage(f.ContentType) ? MediaType.Photo : MediaType.Document).ToList();
        var isAlbum = mediaFiles.Count >= 2 && mediaTypes.Distinct().Count() == 1;

        var results = new List<MediaSendResult>();

        if (isAlbum)
        {
            var mediaType = mediaTypes[0];
            var inputMedia = mediaFiles.Select((f, index) =>
            {
                var caption = index == 0 ? text : null;
                IAlbumInputMedia media = mediaType == MediaType.Photo
                    ? new InputMediaPhoto(new InputFileStream(f.Content, f.FileName)) { Caption = caption }
                    : new InputMediaDocument(new InputFileStream(f.Content, f.FileName)) { Caption = caption };
                return media;
            }).ToList();

            var messages = await _botClient.SendMediaGroup(channelAlias, inputMedia, cancellationToken: cancellationToken);

            foreach (var msg in messages)
            {
                var fileId = mediaType == MediaType.Photo
                    ? msg.Photo!.Last().FileId
                    : msg.Document!.FileId;
                results.Add(new MediaSendResult(fileId, mediaType, msg.MessageId, msg.Chat.Id));
            }

            if (replyMarkup != null)
            {
                await _botClient.SendMessage(channelAlias, "\u200B", replyMarkup: replyMarkup, cancellationToken: cancellationToken);
            }
        }
        else
        {
            foreach (var file in mediaFiles)
            {
                var mediaType = IsImage(file.ContentType) ? MediaType.Photo : MediaType.Document;
                Message message = mediaType == MediaType.Photo
                    ? await _botClient.SendPhoto(channelAlias, new InputFileStream(file.Content, file.FileName), replyMarkup: replyMarkup, caption: text, cancellationToken: cancellationToken)
                    : await _botClient.SendDocument(channelAlias, new InputFileStream(file.Content, file.FileName), replyMarkup: replyMarkup, caption: text, cancellationToken: cancellationToken);

                var fileId = mediaType == MediaType.Photo
                    ? message.Photo!.Last().FileId
                    : message.Document!.FileId;

                results.Add(new MediaSendResult(fileId, mediaType, message.MessageId, message.Chat.Id));
            }
        }

        return results;
    }
    private static bool IsImage(string contentType) => contentType.StartsWith("image/");
    public async Task<(int MessageId, long ChatId)> SendPollAsync(string channelAlias, string question, List<string> options, bool allowMultipleAnswers, bool isAnonymous, int? durationSeconds, CancellationToken cancellationToken = default)
    {
        var message = await _botClient.SendPoll(
        channelAlias,
        question,
        options.Select(o => new InputPollOption(o)),
        isAnonymous: isAnonymous,
        allowsMultipleAnswers: allowMultipleAnswers,
        openPeriod: durationSeconds,
        cancellationToken: cancellationToken);

        return (message.MessageId, message.Chat.Id);
    }

    public async Task StopPollAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        await _botClient.StopPoll(chatId, messageId, cancellationToken: cancellationToken);
    }
    public async Task<bool> DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.DeleteMessage(chatId, messageId, cancellationToken: cancellationToken);
            return true;
        }
        catch (Telegram.Bot.Exceptions.ApiRequestException)
        {
            return false;
        }
    }
    public async Task<bool> CheckBotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _botClient.GetMe(cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
    public async Task<ChannelInfoResult?> GetChannelInfoAsync(string channelAlias, CancellationToken cancellationToken = default)
    {
        try
        {
            var chat = await _botClient.GetChat(channelAlias, cancellationToken: cancellationToken);
            int? memberCount = null;
            try
            {
                memberCount = await _botClient.GetChatMemberCount(channelAlias, cancellationToken: cancellationToken);
            }
            catch { /* не критично, если недоступно */ }

            return new ChannelInfoResult(chat.Id, chat.Title, chat.Type.ToString(), memberCount);
        }
        catch
        {
            return null;
        }
    }
    public async Task<bool> CanPublishToChannelAsync(string channelAlias, CancellationToken cancellationToken = default)
    {
        try
        {
            var chat = await _botClient.GetChat(channelAlias, cancellationToken: cancellationToken);
            var member = await _botClient.GetChatMember(channelAlias, _botClient.BotId, cancellationToken: cancellationToken);
            return member.Status == Telegram.Bot.Types.Enums.ChatMemberStatus.Administrator
                || member.Status == Telegram.Bot.Types.Enums.ChatMemberStatus.Creator;
        }
        catch
        {
            return false;
        }
    }
}