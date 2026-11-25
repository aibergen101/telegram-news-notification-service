namespace TelegramApi.Application.Common;

public record ButtonDto(string Text, string Url);

public record PollOptionDto(string Text);

/// <summary>Файл из запроса: поток читается один раз при отправке в Telegram.</summary>
public record MediaFileDto(string FileName, Stream Content, string ContentType, long Length);
