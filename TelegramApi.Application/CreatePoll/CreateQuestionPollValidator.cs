using FluentValidation;

namespace TelegramApi.Application.Publications.CreatePoll;

public class CreateQuestionPollValidator : AbstractValidator<CreateQuestionPollCommand>
{
    public CreateQuestionPollValidator()
    {
        RuleFor(x => x.ChannelAlias).NotEmpty().WithMessage("ChannelAlias is required");
        RuleFor(x => x.Question).NotEmpty().WithMessage("Question is required");
        RuleFor(x => x.Options)
            .Must(o => o.Count >= 2).WithMessage("Poll must have at least 2 options")
            .Must(o => o.Count <= 10).WithMessage("Poll cannot have more than 10 options");
    }
}