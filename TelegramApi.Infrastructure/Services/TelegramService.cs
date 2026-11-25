using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramApi.Application.Common;
using TelegramApi.Domain;
using MediaType = TelegramApi.Domain.MediaType;
using TextFormat = TelegramApi.Domain.TextFormat;

namespace TelegramApi.Infrastructure;

public class TelegramService(ITelegramBotClient bot, ILogger<TelegramService> logger) : ITelegramService
{
    private static readonly LinkPreviewOptions NoLinkPreview = new() { IsDisabled = true };

    public async Task EnsureCanPublishAsync(string channelAlias, CancellationToken ct)
    {
        var chat = await GetChatOrThrowAsync(channelAlias, ct);

        if (chat.Type != ChatType.Channel)
        {
            throw new ChannelNotFoundException($"'{channelAlias}' is not a Telegram channel.");
        }

        var member = await CallAsync(() => bot.GetChatMember(chat.Id, bot.BotId, ct), ct);

        var canPost = member switch
        {
            ChatMemberOwner => true,
            ChatMemberAdministrator administrator => administrator.CanPostMessages,
            _ => false
        };

        if (!canPost)
        {
            throw new BotLacksRightsException(
                $"The bot cannot post to '{channelAlias}'. Add it as an administrator with the 'Post messages' right.");
        }
    }

    public async Task<SentMessage> SendTextAsync(
        string channelAlias,
        string text,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct)
    {
        var message = await CallAsync(
            () => bot.SendMessage(
                channelAlias,
                text,
                parseMode: ToParseMode(format),
                replyMarkup: BuildKeyboard(buttons),
                linkPreviewOptions: NoLinkPreview,
                cancellationToken: ct),
            ct);

        return new SentMessage(message.MessageId, message.Chat.Id);
    }

    public async Task<IReadOnlyList<MediaSendResult>> SendMediaAsync(
        string channelAlias,
        string? caption,
        TextFormat format,
        IReadOnlyList<MediaFileDto> files,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct)
    {
        var parseMode = ToParseMode(format);

        var mediaType = MediaRules.Classify(files[0].ContentType);

        if (files.Count == 1)
        {
            var file = files[0];
            var input = new InputFileStream(file.Content, file.FileName);

            var message = mediaType == MediaType.Photo
                ? await CallAsync(
                    () => bot.SendPhoto(
                        channelAlias, input, caption, parseMode, replyMarkup: BuildKeyboard(buttons), cancellationToken: ct),
                    ct)
                : await CallAsync(
                    () => bot.SendDocument(
                        channelAlias, input, caption, parseMode, replyMarkup: BuildKeyboard(buttons), cancellationToken: ct),
                    ct);

            return [ToResult(message, mediaType)];
        }

        var album = files
            .Select((file, index) => ToAlbumItem(file, mediaType, index == 0 ? caption : null, parseMode))
            .ToList();

        var messages = await CallAsync(
            () => bot.SendMediaGroup(channelAlias, album, cancellationToken: ct),
            ct);

        return messages.Select(m => ToResult(m, mediaType)).ToList();
    }

    public async Task<SentMessage> SendPollAsync(
        string channelAlias,
        string question,
        IReadOnlyList<string> options,
        bool allowMultipleAnswers,
        bool isAnonymous,
        int? durationSeconds,
        CancellationToken ct)
    {
        var message = await CallAsync(
            () => bot.SendPoll(
                channelAlias,
                question,
                options.Select(text => new InputPollOption(text)).ToList(),
                isAnonymous: isAnonymous,
                allowsMultipleAnswers: allowMultipleAnswers,
                openPeriod: durationSeconds,
                cancellationToken: ct),
            ct);

        return new SentMessage(message.MessageId, message.Chat.Id);
    }

    public async Task StopPollAsync(long chatId, int messageId, CancellationToken ct)
    {
        try
        {
            await CallVoidAsync(() => bot.StopPoll(chatId, messageId, cancellationToken: ct), ct);
        }
        catch (TelegramOperationException ex)
            when (ex.Message.Contains("poll has already been closed", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Poll in message {MessageId} was already closed by Telegram", messageId);
        }
    }

    public Task DeleteMessagesAsync(long chatId, IReadOnlyCollection<int> messageIds, CancellationToken ct) =>
        CallVoidAsync(() => bot.DeleteMessages(chatId, messageIds, ct), ct);

    public Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct) =>
        IgnoreNotModifiedAsync(
            () => bot.EditMessageText(
                chatId,
                messageId,
                text,
                parseMode: ToParseMode(format),
                linkPreviewOptions: NoLinkPreview,
                replyMarkup: BuildKeyboard(buttons),
                cancellationToken: ct),
            ct);

    public Task EditCaptionAsync(
        long chatId,
        int messageId,
        string? caption,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct) =>
        IgnoreNotModifiedAsync(
            () => bot.EditMessageCaption(
                chatId,
                messageId,
                caption,
                parseMode: ToParseMode(format),
                replyMarkup: BuildKeyboard(buttons),
                cancellationToken: ct),
            ct);

    public Task PinMessageAsync(long chatId, int messageId, bool disableNotification, CancellationToken ct) =>
        RequireRightsAsync(
            () => bot.PinChatMessage(chatId, messageId, disableNotification, cancellationToken: ct),
            "pin messages",
            ct);

    public Task UnpinMessageAsync(long chatId, int messageId, CancellationToken ct) =>
        RequireRightsAsync(
            () => bot.UnpinChatMessage(chatId, messageId, cancellationToken: ct),
            "pin messages",
            ct);

    public Task CheckBotAsync(CancellationToken ct) =>
        CallVoidAsync(() => bot.GetMe(ct), ct);

    public async Task<ChannelInfo> GetChannelInfoAsync(string channelAlias, CancellationToken ct)
    {
        var chat = await GetChatOrThrowAsync(channelAlias, ct);

        return new ChannelInfo(chat.Id, chat.Title, chat.Username, chat.Type.ToString());
    }

    private async Task<ChatFullInfo> GetChatOrThrowAsync(string channelAlias, CancellationToken ct)
    {
        try
        {
            return await CallAsync(() => bot.GetChat(channelAlias, ct), ct);
        }
        catch (TelegramOperationException ex) when (ex.ErrorCode is 400 or 403 or 404)
        {
            throw new ChannelNotFoundException(
                $"Channel '{channelAlias}' was not found or is not accessible to the bot.");
        }
    }

    private static InlineKeyboardMarkup? BuildKeyboard(IReadOnlyList<ButtonDto> buttons) =>
        buttons.Count == 0
            ? null
            : new InlineKeyboardMarkup(buttons.Select(b => new[] { InlineKeyboardButton.WithUrl(b.Text, b.Url) }));

    private static ParseMode ToParseMode(TextFormat format) => format switch
    {
        TextFormat.Html => ParseMode.Html,
        TextFormat.MarkdownV2 => ParseMode.MarkdownV2,
        _ => ParseMode.None
    };

    private static IAlbumInputMedia ToAlbumItem(MediaFileDto file, MediaType mediaType, string? caption, ParseMode parseMode)
    {
        var input = new InputFileStream(file.Content, file.FileName);

        return mediaType == MediaType.Photo
            ? new InputMediaPhoto(input) { Caption = caption, ParseMode = parseMode }
            : new InputMediaDocument(input) { Caption = caption, ParseMode = parseMode };
    }

    private static MediaSendResult ToResult(Message message, MediaType mediaType)
    {
        var fileId = mediaType == MediaType.Photo
            ? message.Photo!.Last().FileId
            : message.Document!.FileId;

        return new MediaSendResult(message.MessageId, message.Chat.Id, mediaType, fileId);
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (ApiRequestException ex)
        {
            throw new TelegramOperationException(ex.Message, ex.ErrorCode, innerException: ex);
        }
        catch (RequestException ex)
        {
            var timedOut = ex.InnerException is TaskCanceledException;
            throw new TelegramOperationException(
                timedOut ? "Telegram request timed out." : "Telegram API request failed.",
                isTimeout: timedOut,
                innerException: ex);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TelegramOperationException("Telegram request timed out.", isTimeout: true);
        }
    }

    private async Task IgnoreNotModifiedAsync(Func<Task> call, CancellationToken ct)
    {
        try
        {
            await CallVoidAsync(call, ct);
        }
        catch (TelegramOperationException ex)
            when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Telegram reported that the message was not modified");
        }
    }

    private static async Task RequireRightsAsync(Func<Task> call, string right, CancellationToken ct)
    {
        try
        {
            await CallVoidAsync(call, ct);
        }
        catch (TelegramOperationException ex)
            when (ex.Message.Contains("not enough rights", StringComparison.OrdinalIgnoreCase))
        {
            throw new BotLacksRightsException(
                $"The bot has no right to {right} in this channel. Give it the corresponding administrator right.");
        }
    }

    private static Task CallVoidAsync(Func<Task> call, CancellationToken ct) =>
        CallAsync<object?>(
            async () =>
            {
                await call();
                return null;
            },
            ct);
}
