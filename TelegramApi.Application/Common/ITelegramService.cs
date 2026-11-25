using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public interface ITelegramService
{
    Task EnsureCanPublishAsync(string channelAlias, CancellationToken ct);

    Task<SentMessage> SendTextAsync(
        string channelAlias,
        string text,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct);

    Task<IReadOnlyList<MediaSendResult>> SendMediaAsync(
        string channelAlias,
        string? caption,
        TextFormat format,
        IReadOnlyList<MediaFileDto> files,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct);

    Task<SentMessage> SendPollAsync(
        string channelAlias,
        string question,
        IReadOnlyList<string> options,
        bool allowMultipleAnswers,
        bool isAnonymous,
        int? durationSeconds,
        CancellationToken ct);

    Task StopPollAsync(long chatId, int messageId, CancellationToken ct);

    Task DeleteMessagesAsync(long chatId, IReadOnlyCollection<int> messageIds, CancellationToken ct);

    Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct);

    Task EditCaptionAsync(
        long chatId,
        int messageId,
        string? caption,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct);

    Task PinMessageAsync(long chatId, int messageId, bool disableNotification, CancellationToken ct);

    Task UnpinMessageAsync(long chatId, int messageId, CancellationToken ct);

    Task CheckBotAsync(CancellationToken ct);

    Task<ChannelInfo> GetChannelInfoAsync(string channelAlias, CancellationToken ct);
}

public record SentMessage(int MessageId, long ChatId);

public record MediaSendResult(int MessageId, long ChatId, MediaType MediaType, string TgFileId);

public record ChannelInfo(
    long ChatId,
    string? Title,
    string? Username,
    string Type);
