namespace TelegramApi.Application.Common;

public record ButtonDto(string Text, string Url);

public record PollOptionDto(string Text);

public record MediaFileDto(string FileName, Stream Content, string ContentType, long Length);
