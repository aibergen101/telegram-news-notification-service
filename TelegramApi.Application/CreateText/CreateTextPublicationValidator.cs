using FluentValidation;

namespace TelegramApi.Application.Publications.CreateText;

public class CreateTextPublicationValidator : AbstractValidator<CreateTextPublicationCommand>
{
    public CreateTextPublicationValidator()
    {
        RuleFor(x => x.ChannelAlias).NotEmpty().WithMessage("ChannelAlias is required");
        RuleFor(x => x.Text).NotEmpty().WithMessage("Text is required").MaximumLength(4096).WithMessage("Text cannot exceed 4096 characters");
        RuleForEach(x => x.Buttons).ChildRules(button =>
        {
            button.RuleFor(b => b.Text).NotEmpty().MaximumLength(64);
            button.RuleFor(b => b.Url)
                .NotEmpty()
                .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri)
                           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                .WithMessage("Button URL must be a valid http or https address.");
        }).When(x => x.Buttons is not null);
    }

}