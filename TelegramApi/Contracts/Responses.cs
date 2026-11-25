using System.ComponentModel;
using TelegramApi.Domain;

namespace TelegramApi.Contracts;

[Description("Кнопка со ссылкой под публикацией.")]
public record ButtonResponse(
    [property: Description("Текст кнопки.")] string Text,
    [property: Description("Ссылка кнопки (https).")] string Url);

[Description("Файл публикации. TgFileId и TgMessageId пусты, если Telegram файл не принял (статус Failed).")]
public record MediaFileResponse(
    [property: Description("Имя файла из запроса.")] string FileName,
    [property: Description("Как файл отправлен: Photo — изображение, Document — документ.")] MediaType MediaType,
    [property: Description("Идентификатор файла в Telegram (file_id).")] string? TgFileId,
    [property: Description("Идентификатор сообщения Telegram с этим файлом.")] int? TgMessageId,
    [property: Description("Порядковый номер файла в запросе, с 0.")] int Order);

[Description("Публикация со всеми сохранёнными данными.")]
public record PublicationResponse(
    [property: Description("Внутренний идентификатор публикации.")] Guid Id,
    [property: Description("Канал, в который выполнялась публикация (@username).")] string ChannelAlias,
    [property: Description("Идентификатор чата Telegram; 0, если сообщение не отправлено.")] long TgChatId,
    [property: Description("Идентификатор сообщения Telegram (у альбома — первого); 0, если сообщение не отправлено.")] int TgMessageId,
    [property: Description("Статус: Sent, Failed или Deleted.")] PublicationStatus Status,
    [property: Description("Текст сообщения или подпись к файлам.")] string? Text,
    [property: Description("Ссылка, переданная отдельным полем.")] string? Link,
    [property: Description("Форматирование текста: Plain, Html или MarkdownV2.")] TextFormat Format,
    [property: Description("Кнопки со ссылками в порядке клиента.")] IReadOnlyList<ButtonResponse> Buttons,
    [property: Description("Файлы в порядке клиента; пусто у текстовой публикации.")] IReadOnlyList<MediaFileResponse> MediaFiles,
    [property: Description("Когда запись создана (UTC).")] DateTime CreatedAt,
    [property: Description("Когда Telegram подтвердил отправку (UTC).")] DateTime? SentAt,
    [property: Description("Когда публикация удалена (UTC).")] DateTime? DeletedAt,
    [property: Description("Когда запись последний раз изменялась (UTC).")] DateTime? UpdatedAt,
    [property: Description("Когда сообщение последний раз редактировалось (UTC).")] DateTime? EditedAt,
    [property: Description("Сообщение закреплено в канале.")] bool IsPinned,
    [property: Description("Когда сообщение закреплено (UTC).")] DateTime? PinnedAt,
    [property: Description("Код последней ошибки Telegram, если публикация не удалась.")] int? ErrorCode,
    [property: Description("Текст последней ошибки.")] string? ErrorText,
    [property: Description("Когда произошла последняя ошибка (UTC).")] DateTime? ErrorDate);

[Description("Вариант ответа в опросе.")]
public record PollOptionResponse(
    [property: Description("Текст варианта.")] string Text,
    [property: Description("Порядковый номер варианта, с 0.")] int Order);

[Description("Опрос со всеми сохранёнными данными.")]
public record PollResponse(
    [property: Description("Внутренний идентификатор опроса.")] Guid Id,
    [property: Description("Канал, в который выполнялась публикация (@username).")] string ChannelAlias,
    [property: Description("Идентификатор чата Telegram; 0, если опрос не отправлен.")] long TgChatId,
    [property: Description("Идентификатор сообщения Telegram; 0, если опрос не отправлен.")] int TgMessageId,
    [property: Description("Статус: Sent, Failed или Deleted.")] PublicationStatus Status,
    [property: Description("Вопрос опроса.")] string Question,
    [property: Description("Варианты ответа в порядке клиента.")] IReadOnlyList<PollOptionResponse> Options,
    [property: Description("Можно выбрать несколько вариантов.")] bool AllowMultipleAnswers,
    [property: Description("Анонимный опрос.")] bool IsAnonymous,
    [property: Description("Продолжительность в секундах; null — без ограничения по времени.")] int? Duration,
    [property: Description("Опрос закрыт досрочно через API.")] bool IsClosed,
    [property: Description("Когда запись создана (UTC).")] DateTime CreatedAt,
    [property: Description("Когда Telegram подтвердил отправку (UTC).")] DateTime? SentAt,
    [property: Description("Когда опрос закрыт (UTC).")] DateTime? ClosedAt,
    [property: Description("Когда опрос удалён (UTC).")] DateTime? DeletedAt,
    [property: Description("Когда запись последний раз изменялась (UTC).")] DateTime? UpdatedAt,
    [property: Description("Сообщение закреплено в канале.")] bool IsPinned,
    [property: Description("Когда сообщение закреплено (UTC).")] DateTime? PinnedAt,
    [property: Description("Код последней ошибки Telegram, если публикация не удалась.")] int? ErrorCode,
    [property: Description("Текст последней ошибки.")] string? ErrorText,
    [property: Description("Когда произошла последняя ошибка (UTC).")] DateTime? ErrorDate);

[Description("Запись единой истории: публикация или опрос.")]
public record HistoryItemResponse(
    [property: Description("Внутренний идентификатор записи. Подробности: GET /api/publications/{id} или GET /api/polls/{id} в зависимости от Type.")] Guid Id,
    [property: Description("Post — обычная публикация, Poll — опрос.")] PublicationKind Type,
    [property: Description("Канал (@username).")] string ChannelAlias,
    [property: Description("Текст публикации или вопрос опроса.")] string? Text,
    [property: Description("Статус: Sent, Failed или Deleted.")] PublicationStatus Status,
    [property: Description("Когда запись создана (UTC).")] DateTime CreatedAt,
    [property: Description("Когда Telegram подтвердил отправку (UTC).")] DateTime? SentAt,
    [property: Description("Когда запись удалена (UTC).")] DateTime? DeletedAt);

[Description("Страница списка.")]
public record PagedResponse<T>(
    [property: Description("Записи текущей страницы, новые сначала.")] IReadOnlyList<T> Items,
    [property: Description("Номер страницы, с 1.")] int Page,
    [property: Description("Размер страницы.")] int PageSize,
    [property: Description("Всего записей с учётом фильтров.")] int TotalCount);

[Description("Результат проверки бота.")]
public record BotCheckResponse(
    [property: Description("ok — Telegram доступен и токен бота действителен.")] string Status);

[Description("Информация о канале Telegram.")]
public record ChannelInfoResponse(
    [property: Description("Идентификатор чата Telegram.")] long ChatId,
    [property: Description("Название канала.")] string? Title,
    [property: Description("Публичное имя канала без @.")] string? Username,
    [property: Description("Тип чата в Telegram, для канала — Channel.")] string Type);
