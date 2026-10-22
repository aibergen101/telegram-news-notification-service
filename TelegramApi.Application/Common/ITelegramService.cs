using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;
public interface ITelegramService
{
    Task<(int MessageId, long ChatId)> SendTextMessageAsync(string channelAlias, string text, List<ButtonDto>? buttons, CancellationToken cancellationToken = default);
    Task<(int MessageId, long ChatId)> SendPollAsync(string channelAlias, string question, List<string> options, bool allowMultipleAnswers, bool isAnonymous, int? durationSeconds, CancellationToken cancellationToken = default);
    Task StopPollAsync(long chatId, int messageId, CancellationToken cancellationToken = default);
    Task<List<MediaSendResult>> SendMediaAsync(string channelAlias, string? text, List<ButtonDto>? buttons, List<MediaFileDto> mediaFiles, CancellationToken cancellationToken = default);
    Task<bool> DeleteMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default);
    Task<bool> CheckBotAsync(CancellationToken cancellationToken = default);
    Task<ChannelInfoResult?> GetChannelInfoAsync(string channelAlias, CancellationToken cancellationToken = default);
    Task<bool> CanPublishToChannelAsync(string channelAlias, CancellationToken cancellationToken = default);
}