namespace TelegramApi.Application.Common;

public record ChannelInfoResult(long ChatId, string? Title, string? Type, int? MemberCount);