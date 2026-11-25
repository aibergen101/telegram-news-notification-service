using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Validators;

public class CreateTextPublicationValidatorTests
{
    private readonly CreateTextPublicationValidator _validator = new();

    private static CreateTextPublicationCommand Valid(
        string alias = "@my_channel",
        string text = "Hello",
        TextFormat format = TextFormat.Plain,
        IReadOnlyList<ButtonDto>? buttons = null) =>
        new(alias, text, format, buttons ?? []);

    [Theory]
    [InlineData("@my_channel")]
    [InlineData("my_channel")]
    [InlineData("Abcde")]
    public void Accepts_valid_command(string alias)
    {
        var result = _validator.Validate(Valid(alias: alias));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("@abc")]
    [InlineData("@1channel")]
    [InlineData("@my-channel")]
    [InlineData("@abcdefghijklmnopqrstuvwxyz1234567")]
    public void Rejects_invalid_channel_alias(string alias)
    {
        var result = _validator.Validate(Valid(alias: alias));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTextPublicationCommand.ChannelAlias));
    }

    [Fact]
    public void Rejects_empty_text()
    {
        var result = _validator.Validate(Valid(text: ""));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTextPublicationCommand.Text));
    }

    [Fact]
    public void Accepts_text_at_limit_and_rejects_longer()
    {
        Assert.True(_validator.Validate(Valid(text: new string('a', TelegramLimits.MaxMessageLength))).IsValid);
        Assert.False(_validator.Validate(Valid(text: new string('a', TelegramLimits.MaxMessageLength + 1))).IsValid);
    }

    [Fact]
    public void Rejects_unknown_format()
    {
        var result = _validator.Validate(Valid(format: (TextFormat)42));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTextPublicationCommand.Format));
    }

    [Fact]
    public void Rejects_too_many_buttons()
    {
        var buttons = Enumerable.Range(0, TelegramLimits.MaxButtons + 1)
            .Select(i => new ButtonDto($"b{i}", "https://example.com"))
            .ToList();

        var result = _validator.Validate(Valid(buttons: buttons));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTextPublicationCommand.Buttons));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public void Rejects_button_without_https_url(string url)
    {
        var result = _validator.Validate(Valid(buttons: [new ButtonDto("Open", url)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Buttons[0].Url");
    }

    [Fact]
    public void Rejects_button_with_empty_or_too_long_text()
    {
        var tooLong = new string('a', TelegramLimits.MaxButtonTextLength + 1);

        var result = _validator.Validate(Valid(buttons:
        [
            new ButtonDto("", "https://example.com"),
            new ButtonDto(tooLong, "https://example.com")
        ]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Buttons[0].Text");
        Assert.Contains(result.Errors, e => e.PropertyName == "Buttons[1].Text");
    }
}
