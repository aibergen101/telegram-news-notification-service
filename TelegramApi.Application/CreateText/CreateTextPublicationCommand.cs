using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreateText;
public record CreateTextPublicationCommand(string ChannelAlias, string Text,List<ButtonDto>? Buttons);