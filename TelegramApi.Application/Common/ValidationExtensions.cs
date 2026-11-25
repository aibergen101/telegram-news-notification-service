using FluentValidation;

namespace TelegramApi.Application.Common;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeChannelAlias<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches(ChannelAlias.Pattern)
            .WithMessage("Channel alias must look like @my_channel: 5–32 characters, letters, digits and underscore, starting with a letter.");

    public static IRuleBuilderOptions<T, string?> MustBeIdempotencyKey<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(key => key is null || (key.Length <= Idempotency.MaxKeyLength && !string.IsNullOrWhiteSpace(key)))
            .WithName("Idempotency-Key")
            .WithMessage($"Idempotency-Key must be a non-blank string of up to {Idempotency.MaxKeyLength} characters.");
}

/// <summary>Правила одной кнопки: текст, и ссылка только по https (ТЗ 2.10, 9.7).</summary>
public class ButtonDtoValidator : AbstractValidator<ButtonDto>
{
    public ButtonDtoValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty()
            .MaximumLength(TelegramLimits.MaxButtonTextLength);

        RuleFor(x => x.Url)
            .NotEmpty()
            .MaximumLength(TelegramLimits.MaxButtonUrlLength)
            .Must(BeAbsoluteHttpsUrl)
            .WithMessage("Button URL must be an absolute https address.");
    }

    private static bool BeAbsoluteHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
