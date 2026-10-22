namespace TelegramApi.Application.Common;
public record MediaFileDto(string FileName, Stream Content, string ContentType);