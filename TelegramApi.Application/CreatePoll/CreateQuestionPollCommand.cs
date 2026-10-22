using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreatePoll;

public record CreateQuestionPollCommand(
    string ChannelAlias,
    string Question,
    List<PollOptionDto> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    int? Duration);