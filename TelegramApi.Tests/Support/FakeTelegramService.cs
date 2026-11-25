using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Support;

/// <summary>Записывает вызовы вместо обращения к Telegram.</summary>
public sealed class FakeTelegramService : ITelegramService
{
    public const long ChatId = -100123;

    private int nextMessageId = 100;

    public List<string> Calls { get; } = [];

    public List<byte[]> SentFileContents { get; } = [];

    /// <summary>Если задано, любой вызов, меняющий сообщения, бросает это исключение.</summary>
    public Exception? FailWith { get; set; }

    public Task EnsureCanPublishAsync(string channelAlias, CancellationToken ct) => Task.CompletedTask;

    public Task<SentMessage> SendTextAsync(
        string channelAlias, string text, TextFormat format, IReadOnlyList<ButtonDto> buttons, CancellationToken ct)
    {
        Record($"SendText:{text}");
        return Task.FromResult(new SentMessage(nextMessageId++, ChatId));
    }

    public async Task<IReadOnlyList<MediaSendResult>> SendMediaAsync(
        string channelAlias, string? caption, TextFormat format, IReadOnlyList<MediaFileDto> files, CancellationToken ct)
    {
        Record($"SendMedia:{files.Count}");
        var results = new List<MediaSendResult>();
        foreach (var file in files)
        {
            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, ct);
            SentFileContents.Add(buffer.ToArray());
            results.Add(new MediaSendResult(nextMessageId++, ChatId, MediaRules.Classify(file.ContentType), "file-id"));
        }

        return results;
    }

    public Task<SentMessage> SendPollAsync(
        string channelAlias, string question, IReadOnlyList<string> options, bool allowMultipleAnswers,
        bool isAnonymous, int? durationSeconds, CancellationToken ct)
    {
        Record($"SendPoll:{question}");
        return Task.FromResult(new SentMessage(nextMessageId++, ChatId));
    }

    public Task EditTextAsync(
        long chatId, int messageId, string text, TextFormat format, IReadOnlyList<ButtonDto> buttons, CancellationToken ct)
    {
        Record($"EditText:{messageId}:{text}:{buttons.Count}");
        return Task.CompletedTask;
    }

    public Task EditCaptionAsync(long chatId, int messageId, string? caption, TextFormat format, CancellationToken ct)
    {
        Record($"EditCaption:{messageId}:{caption}");
        return Task.CompletedTask;
    }

    public Task PinMessageAsync(long chatId, int messageId, bool disableNotification, CancellationToken ct)
    {
        Record($"Pin:{messageId}:{disableNotification}");
        return Task.CompletedTask;
    }

    public Task UnpinMessageAsync(long chatId, int messageId, CancellationToken ct)
    {
        Record($"Unpin:{messageId}");
        return Task.CompletedTask;
    }

    public Task StopPollAsync(long chatId, int messageId, CancellationToken ct) => Task.CompletedTask;

    public Task DeleteMessagesAsync(long chatId, IReadOnlyCollection<int> messageIds, CancellationToken ct) =>
        Task.CompletedTask;

    public Task CheckBotAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<ChannelInfo> GetChannelInfoAsync(string channelAlias, CancellationToken ct) =>
        Task.FromResult(new ChannelInfo(ChatId, "Test", channelAlias, "Channel"));

    private void Record(string call)
    {
        if (FailWith is not null)
        {
            throw FailWith;
        }

        Calls.Add(call);
    }
}
