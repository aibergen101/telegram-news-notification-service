using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public record MediaSendResult(string TgFileId, MediaType MediaType, int MessageId, long ChatId);