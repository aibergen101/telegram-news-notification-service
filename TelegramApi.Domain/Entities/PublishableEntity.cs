namespace TelegramApi.Domain;

/// <summary>
/// Общая часть публикации и опроса: данные Telegram, статус, даты и последняя ошибка.
/// Сам по себе в БД не отображается: поля попадают в таблицы наследников.
/// </summary>
public abstract class PublishableEntity
{
    public const int MaxErrorTextLength = 1000;

    public const int MaxIdempotencyKeyLength = 255;

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

    /// <summary>Сообщение закреплено в канале (для альбома закрепляется первое сообщение).</summary>
    public bool IsPinned { get; set; }

    public DateTime? PinnedAt { get; set; }

    /// <summary>Ключ идемпотентности, переданный клиентом при создании. Уникален в пределах таблицы.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>SHA-256 содержимого запроса создания: по нему повтор с тем же ключом отличается от другого запроса.</summary>
    public string? IdempotencyHash { get; set; }

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

    public void MarkDeleted(DateTime now)
    {
        Status = PublicationStatus.Deleted;
        DeletedAt = now;
        IsPinned = false;
        PinnedAt = null;
        UpdatedAt = now;
    }

    public void MarkPinned(DateTime now)
    {
        IsPinned = true;
        PinnedAt = now;
        UpdatedAt = now;
    }

    public void MarkUnpinned(DateTime now)
    {
        IsPinned = false;
        PinnedAt = null;
        UpdatedAt = now;
    }
}
