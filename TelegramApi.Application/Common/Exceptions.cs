namespace TelegramApi.Application.Common;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ChannelNotFoundException(string message) : Exception(message);

public sealed class BotLacksRightsException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

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

    public int? ErrorCode { get; }

    public bool IsTimeout { get; }
}
