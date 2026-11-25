using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.CreatePoll;

namespace TelegramApi.Tests.Validators;

public class CreateQuestionPollValidatorTests
{
    private readonly CreateQuestionPollValidator _validator = new();

    private static CreateQuestionPollCommand Command(
        string question = "Question?",
        int optionCount = 2,
        int? duration = null,
        string alias = "@my_channel") =>
        new(
            alias,
            question,
            Enumerable.Range(0, optionCount).Select(i => new PollOptionDto($"Option {i}")).ToList(),
            AllowMultipleAnswers: false,
            IsAnonymous: true,
            duration);

    [Fact]
    public void Accepts_valid_poll()
    {
        Assert.True(_validator.Validate(Command()).IsValid);
    }

    [Fact]
    public void Rejects_invalid_alias()
    {
        var result = _validator.Validate(Command(alias: "bad alias"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateQuestionPollCommand.ChannelAlias));
    }

    [Fact]
    public void Rejects_empty_or_too_long_question()
    {
        Assert.False(_validator.Validate(Command(question: "")).IsValid);
        Assert.True(_validator.Validate(Command(question: new string('q', TelegramLimits.MaxPollQuestionLength))).IsValid);
        Assert.False(_validator.Validate(Command(question: new string('q', TelegramLimits.MaxPollQuestionLength + 1))).IsValid);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Checks_option_count(int count, bool expectedValid)
    {
        Assert.Equal(expectedValid, _validator.Validate(Command(optionCount: count)).IsValid);
    }

    [Fact]
    public void Rejects_empty_or_too_long_option()
    {
        var command = Command() with
        {
            Options = [new PollOptionDto(""), new PollOptionDto(new string('o', TelegramLimits.MaxPollOptionLength + 1))]
        };

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors, e => e.PropertyName == "Options[0].Text");
        Assert.Contains(result.Errors, e => e.PropertyName == "Options[1].Text");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(600, true)]
    [InlineData(601, false)]
    public void Checks_duration(int? duration, bool expectedValid)
    {
        Assert.Equal(expectedValid, _validator.Validate(Command(duration: duration)).IsValid);
    }
}
