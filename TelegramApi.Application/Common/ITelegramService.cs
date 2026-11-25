using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

/// <summary>
/// Единственная точка общения с Telegram Bot API.
/// Все ошибки Telegram приходят как TelegramOperationException, ChannelNotFoundException или BotLacksRightsException.
/// </summary>
public interface ITelegramService
{
    /// <summary>Проверяет, что канал существует и бот может в него публиковать.</summary>
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
        CancellationToken ct);

    Task<SentMessage> SendPollAsync(
        string channelAlias,
        string question,
        IReadOnlyList<string> options,
        bool allowMultipleAnswers,
        bool isAnonymous,
        int? durationSeconds,
        CancellationToken ct);

    /// <summary>
    /// Меняет текст и кнопки текстового сообщения. Пустой список кнопок убирает клавиатуру.
    /// Если содержимое не изменилось, считается успехом.
    /// </summary>
    Task EditTextAsync(
        long chatId,
        int messageId,
        string text,
        TextFormat format,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct);

    /// <summary>Меняет подпись медиа-сообщения; null убирает подпись. Если подпись не изменилась, считается успехом.</summary>
    Task EditCaptionAsync(long chatId, int messageId, string? caption, TextFormat format, CancellationToken ct);

    Task PinMessageAsync(long chatId, int messageId, bool disableNotification, CancellationToken ct);

    Task UnpinMessageAsync(long chatId, int messageId, CancellationToken ct);

    /// <summary>Закрывает опрос; если Telegram уже закрыл его сам, считается успехом.</summary>
    Task StopPollAsync(long chatId, int messageId, CancellationToken ct);

    Task DeleteMessagesAsync(long chatId, IReadOnlyCollection<int> messageIds, CancellationToken ct);

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
