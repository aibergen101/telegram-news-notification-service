using System.Collections.Concurrent;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Integration;

/// <summary>Подменяет Telegram в интеграционных тестах: запоминает вызовы и может имитировать ошибки.</summary>
public sealed class FakeTelegramService : ITelegramService
{
    public const long ChatId = -100123456789;

    private int nextMessageId = 100;

    public ConcurrentQueue<string> Calls { get; } = new();

    /// <summary>Исключение, которое бросит следующая операция отправки/изменения (не проверка канала).</summary>
    public Exception? FailNextSend { get; set; }

    /// <summary>Исключение для проверки канала перед публикацией.</summary>
    public Exception? FailEnsureCanPublish { get; set; }

    /// <summary>Кнопки, переданные в последнюю отправку файлов.</summary>
    public IReadOnlyList<ButtonDto>? LastMediaButtons { get; private set; }

    public int CountOf(string operation) => Calls.Count(c => c == operation);

    public void Reset()
    {
        Calls.Clear();
        FailNextSend = null;
        FailEnsureCanPublish = null;
        LastMediaButtons = null;
    }

    public Task EnsureCanPublishAsync(string channelAlias, CancellationToken ct)
    {
        Calls.Enqueue(nameof(EnsureCanPublishAsync));
        return FailEnsureCanPublish is { } ex ? Task.FromException(ex) : Task.CompletedTask;
    }

    public Task<SentMessage> SendTextAsync(
        string channelAlias, string text, TextFormat format, IReadOnlyList<ButtonDto> buttons, CancellationToken ct)
    {
        Record(nameof(SendTextAsync));
        return Task.FromResult(new SentMessage(NextId(), ChatId));
    }

    public async Task<IReadOnlyList<MediaSendResult>> SendMediaAsync(
        string channelAlias,
        string? caption,
        TextFormat format,
        IReadOnlyList<MediaFileDto> files,
        IReadOnlyList<ButtonDto> buttons,
        CancellationToken ct)
    {
        Record(nameof(SendMediaAsync));
        LastMediaButtons = buttons;

        var results = new List<MediaSendResult>();
        foreach (var file in files)
        {
            // Как и настоящий клиент, читаем поток целиком.
            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, ct);

            results.Add(new MediaSendResult(
                NextId(), ChatId, MediaRules.Classify(file.ContentType), $"file-{Guid.NewGuid():N}"));
        }

        return results;
    }

    public Task<SentMessage> SendPollAsync(
        string channelAlias,
        string question,
        IReadOnlyList<string> options,
        bool allowMultipleAnswers,
        bool isAnonymous,
        int? durationSeconds,
        CancellationToken ct)
    {
        Record(nameof(SendPollAsync));
        return Task.FromResult(new SentMessage(NextId(), ChatId));
    }

    public Task StopPollAsync(long chatId, int messageId, CancellationToken ct) => Done(nameof(StopPollAsync));

    public Task DeleteMessagesAsync(long chatId, IReadOnlyCollection<int> messageIds, CancellationToken ct) =>
        Done(nameof(DeleteMessagesAsync));

    public Task EditTextAsync(
        long chatId, int messageId, string text, TextFormat format, IReadOnlyList<ButtonDto> buttons, CancellationToken ct) =>
        Done(nameof(EditTextAsync));

    public Task EditCaptionAsync(
        long chatId, int messageId, string? caption, TextFormat format, IReadOnlyList<ButtonDto> buttons, CancellationToken ct) =>
        Done(nameof(EditCaptionAsync));

    public Task PinMessageAsync(long chatId, int messageId, bool disableNotification, CancellationToken ct) =>
        Done(nameof(PinMessageAsync));

    public Task UnpinMessageAsync(long chatId, int messageId, CancellationToken ct) => Done(nameof(UnpinMessageAsync));

    public Task CheckBotAsync(CancellationToken ct) => Done(nameof(CheckBotAsync));

    public Task<ChannelInfo> GetChannelInfoAsync(string channelAlias, CancellationToken ct)
    {
        Record(nameof(GetChannelInfoAsync));
        return Task.FromResult(new ChannelInfo(ChatId, "Test channel", channelAlias.TrimStart('@'), "Channel"));
    }

    private Task Done(string operation)
    {
        Record(operation);
        return Task.CompletedTask;
    }

    private void Record(string operation)
    {
        Calls.Enqueue(operation);
        if (FailNextSend is { } ex)
        {
            FailNextSend = null;
            throw ex;
        }
    }

    private int NextId() => Interlocked.Increment(ref nextMessageId);
}
