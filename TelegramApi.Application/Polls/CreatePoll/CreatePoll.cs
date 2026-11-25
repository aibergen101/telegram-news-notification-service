using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Polls.CreatePoll;

/// <param name="Duration">Продолжительность опроса в секундах; null, если опрос не ограничен по времени.</param>
public record CreateQuestionPollCommand(
    string ChannelAlias,
    string Question,
    IReadOnlyList<PollOptionDto> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    int? Duration);

public class CreateQuestionPollValidator : AbstractValidator<CreateQuestionPollCommand>
{
    public CreateQuestionPollValidator()
    {
        RuleFor(x => x.ChannelAlias).MustBeChannelAlias();

        RuleFor(x => x.Question)
            .NotEmpty()
            .MaximumLength(TelegramLimits.MaxPollQuestionLength);

        RuleFor(x => x.Options)
            .Must(options => options.Count is >= TelegramLimits.MinPollOptions and <= TelegramLimits.MaxPollOptions)
            .WithMessage($"A poll must have from {TelegramLimits.MinPollOptions} to {TelegramLimits.MaxPollOptions} options.");

        RuleForEach(x => x.Options).ChildRules(option =>
            option.RuleFor(o => o.Text)
                .NotEmpty()
                .MaximumLength(TelegramLimits.MaxPollOptionLength));

        RuleFor(x => x.Duration)
            .Must(duration => duration is >= TelegramLimits.MinPollDurationSeconds and <= TelegramLimits.MaxPollDurationSeconds)
            .When(x => x.Duration.HasValue)
            .WithMessage($"Duration must be from {TelegramLimits.MinPollDurationSeconds} to {TelegramLimits.MaxPollDurationSeconds} seconds.");
    }
}

public class CreateQuestionPollHandler
{
    public static async Task<PollDetails> Handle(
        CreateQuestionPollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateQuestionPollHandler> logger,
        CancellationToken ct)
    {
        var alias = ChannelAlias.Normalize(command.ChannelAlias);
        await telegram.EnsureCanPublishAsync(alias, ct);

        var poll = new Poll
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            Question = command.Question,
            AllowMultipleAnswers = command.AllowMultipleAnswers,
            IsAnonymous = command.IsAnonymous,
            Duration = command.Duration,
            CreatedAt = DateTime.UtcNow,
            Options = command.Options.ToEntities()
        };

        db.Polls.Add(poll);
        await db.SaveChangesAsync(ct);

        await PublishFlow.ExecuteAsync(poll, db, logger, async () =>
        {
            var sent = await telegram.SendPollAsync(
                alias,
                command.Question,
                command.Options.Select(o => o.Text).ToList(),
                command.AllowMultipleAnswers,
                command.IsAnonymous,
                command.Duration,
                ct);

            poll.MarkSent(sent.ChatId, sent.MessageId, DateTime.UtcNow);
        });

        return poll.ToDetails();
    }
}
