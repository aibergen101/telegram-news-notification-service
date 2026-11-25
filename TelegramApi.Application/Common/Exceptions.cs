namespace TelegramApi.Application.Common;

/// <summary>Запрошенная публикация или опрос не найдены (404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Канал не существует, недоступен боту или не является каналом (404).</summary>
public sealed class ChannelNotFoundException(string message) : Exception(message);

/// <summary>У бота нет права публиковать в канале (403).</summary>
public sealed class BotLacksRightsException(string message) : Exception(message);

/// <summary>Операция невозможна в текущем состоянии записи (409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Telegram отклонил запрос, не ответил вовремя или недоступен (422, 502 или 504).</summary>
public sealed class TelegramOperationException : Exception
{
    public TelegramOperationException(
        string message,
        int? errorCode = null,
        bool isTimeout = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        IsTimeout = isTimeout;
    }

    /// <summary>Код ошибки Telegram Bot API, если он есть.</summary>
    public int? ErrorCode { get; }

    public bool IsTimeout { get; }
}
