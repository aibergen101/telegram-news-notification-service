namespace TelegramApi.Domain;

public class MediaFile
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public string TgFileId { get; set; } = string.Empty;

    /// <summary>Идентификатор сообщения Telegram, в котором отправлен файл (для альбома у каждого файла свой).</summary>
    public int TgMessageId { get; set; }

    /// <summary>Порядок файла в запросе клиента.</summary>
    public int Order { get; set; }

    public Guid PublicationId { get; set; }

    public Publication Publication { get; set; } = null!;
}
