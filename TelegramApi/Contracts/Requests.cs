using System.ComponentModel;
using TelegramApi.Domain;

namespace TelegramApi.Contracts;

[Description("Кнопка со ссылкой под публикацией.")]
public record ButtonRequest(
    [property: Description("Текст кнопки, 1–64 символа.")] string Text,
    [property: Description("Абсолютная ссылка, только https.")] string Url);

[Description("Запрос на создание текстовой публикации.")]
public record CreateTextPublicationRequest(
    [property: Description("Канал: @username.")] string ChannelAlias,
    [property: Description("Текст сообщения. Обязателен, если не задана ссылка Link. Вместе со ссылкой до 4096 символов.")] string? Text,
    [property: Description("Форматирование текста: Plain, Html или MarkdownV2.")] TextFormat Format = TextFormat.Plain,
    [property: Description("Кнопки со ссылками, до 100 штук.")] List<ButtonRequest>? Buttons = null,
    [property: Description("Ссылка отдельным полем: абсолютный http(s) адрес до 2048 символов. Добавляется отдельной строкой в конец текста; превью ссылки не показывается.")] string? Link = null);

[Description("Новое содержимое публикации. Текст, формат и кнопки заменяются целиком; файлы не меняются.")]
public record EditPublicationRequest(
    [property: Description("Новый текст (1–4096 символов) или подпись к файлам (до 1024 символов, null убирает подпись).")] string? Text,
    [property: Description("Форматирование: Plain, Html или MarkdownV2.")] TextFormat Format = TextFormat.Plain,
    [property: Description("Кнопки со ссылками, до 100 штук. Пустой список убирает кнопки. Для альбома кнопки недоступны.")] List<ButtonRequest>? Buttons = null,
    [property: Description("Ссылка отдельным полем: абсолютный http(s) адрес. Добавляется в конец текста или подписи; null убирает ссылку.")] string? Link = null);

[Description("Вариант ответа в опросе.")]
public record PollOptionRequest(
    [property: Description("Текст варианта, 1–100 символов.")] string Text);

[Description("Запрос на создание опроса.")]
public record CreatePollRequest(
    [property: Description("Канал: @username.")] string ChannelAlias,
    [property: Description("Вопрос, 1–300 символов.")] string Question,
    [property: Description("От 2 до 10 вариантов ответа.")] List<PollOptionRequest> Options,
    [property: Description("Разрешить выбор нескольких ответов.")] bool AllowMultipleAnswers = false,
    [property: Description("Анонимный опрос (по умолчанию да).")] bool IsAnonymous = true,
    [property: Description("Продолжительность в секундах, от 5 до 600. Без значения опрос не ограничен по времени.")] int? Duration = null);

[Description("Форма для публикации с файлами (multipart/form-data).")]
public class CreateMediaPublicationRequest
{
    [Description("Канал: @username.")]
    public string ChannelAlias { get; set; } = string.Empty;

    [Description("Подпись к файлам, до 1024 символов. Необязательно.")]
    public string? Text { get; set; }

    [Description("Ссылка отдельным полем: абсолютный http(s) адрес. Добавляется отдельной строкой в конец подписи; вместе с подписью до 1024 символов.")]
    public string? Link { get; set; }

    [Description("Форматирование подписи: Plain, Html или MarkdownV2.")]
    public TextFormat Format { get; set; } = TextFormat.Plain;

    [Description("Файлы: один файл или альбом из 2–10 файлов одного вида (изображения или документы).")]
    public IFormFileCollection Files { get; set; } = new FormFileCollection();

    /// <summary>
    /// В multipart-форме кнопки передаются одним полем с JSON-массивом: так их можно заполнить в Scalar
    /// и любом HTTP-клиенте одной строкой.
    /// </summary>
    [Description("Кнопки со ссылками: JSON-массив, например [{\"text\":\"Open\",\"url\":\"https://example.com\"}], до 100 штук. Только для одного файла: к альбому Telegram их не прикрепляет.")]
    public string? Buttons { get; set; }
}
