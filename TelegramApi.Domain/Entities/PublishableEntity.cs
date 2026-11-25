namespace TelegramApi.Domain;

/// <summary>
/// Общая часть публикации и опроса: данные Telegram, статус, даты и последняя ошибка.
/// Сам по себе в БД не отображается: поля попадают в таблицы наследников.
/// </summary>
public abstract class PublishableEntity
{
    public const int MaxErrorTextLength = 1000;

    public Guid Id { get; set; }

    public string TgAlias { get; set; } = string.Empty;

    public long TgChatId { get; set; }

    public int TgMsgId { get; set; }

    /// <summary>
    /// Пока Telegram не подтвердил отправку, запись считается Failed
    /// (отдельного статуса «в процессе» в ТЗ нет).
    /// </summary>
    public PublicationStatus Status { get; set; } = PublicationStatus.Failed;

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? ErrorCode { get; set; }

    public string? ErrorText { get; set; }

    public DateTime? ErrorDate { get; set; }

    /// <summary>Закреплено ли сообщение в канале (по последней операции через API).</summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Ключ идемпотентности из заголовка Idempotency-Key. Хранится бессрочно, уникален в таблице.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>SHA-256 содержимого запроса, с которым был использован ключ идемпотентности.</summary>
    public string? RequestHash { get; set; }

    public void MarkSent(long chatId, int messageId, DateTime now)
    {
        TgChatId = chatId;
        TgMsgId = messageId;
        Status = PublicationStatus.Sent;
        SentAt = now;
        UpdatedAt = now;
    }

    public void MarkFailed(int? errorCode, string errorText, DateTime now)
    {
        Status = PublicationStatus.Failed;
        ErrorCode = errorCode;
        ErrorText = errorText.Length > MaxErrorTextLength ? errorText[..MaxErrorTextLength] : errorText;
        ErrorDate = now;
        UpdatedAt = now;
    }

    public void MarkPinned(bool isPinned, DateTime now)
    {
        IsPinned = isPinned;
        UpdatedAt = now;
    }

    public void MarkDeleted(DateTime now)
    {
        Status = PublicationStatus.Deleted;
        DeletedAt = now;
        IsPinned = false;
        UpdatedAt = now;
    }
}
