using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreateMedia;
public record CreateMediaPublicationCommand(string ChannelAlias, string? Text, List<ButtonDto>? Buttons, List<MediaFileDto> MediaFiles);