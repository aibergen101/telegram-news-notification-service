namespace TelegramApi.Contracts;

public record CreatePollRequest(
    string ChannelAlias,
    string Question,
    List<PollOptionRequest> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    int? Duration);