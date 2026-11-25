using FluentValidation;

namespace TelegramApi.Application.Common;

public static class ValidationExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeChannelAlias<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Matches(ChannelAlias.Pattern)
            .WithMessage("Channel alias must look like @my_channel: 5–32 characters, letters, digits and underscore, starting with a letter.");

    /// <summary>Ссылка из отдельного поля публикации (ТЗ 2.8): абсолютный http или https адрес.</summary>
    public static IRuleBuilderOptions<T, string?> MustBeLink<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(TelegramLimits.MaxLinkLength)
            .Must(link => link is null || IsWebUrl(link))
            .WithMessage("Link must be an absolute http or https address.");

    private static bool IsWebUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
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
