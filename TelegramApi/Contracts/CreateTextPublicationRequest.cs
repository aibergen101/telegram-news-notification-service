namespace TelegramApi.Contracts;
public record CreateTextPublicationRequest(string ChannelAlias, string Text,List<ButtonApi>? Button);