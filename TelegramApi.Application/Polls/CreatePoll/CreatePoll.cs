using FluentValidation;
using Microsoft.Extensions.Logging;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Application.Polls.CreatePoll;

/// <param name="Duration">Продолжительность опроса в секундах; null, если опрос не ограничен по времени.</param>
/// <param name="IdempotencyKey">Необязательный ключ идемпотентности из заголовка Idempotency-Key.</param>
public record CreateQuestionPollCommand(
    string ChannelAlias,
    string Question,
    IReadOnlyList<PollOptionDto> Options,
    bool AllowMultipleAnswers,
    bool IsAnonymous,
    int? Duration,
    string? IdempotencyKey = null);

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

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey().When(x => x.IdempotencyKey is not null);
    }
}

public class CreateQuestionPollHandler
{
    public static async Task<CreateResult<PollDetails>> Handle(
        CreateQuestionPollCommand command,
        IApplicationDbContext db,
        ITelegramService telegram,
        ILogger<CreateQuestionPollHandler> logger,
        CancellationToken ct)
    {
        var alias = command.ChannelAlias;

        string? hash = null;
        if (command.IdempotencyKey is { } key)
        {
            hash = Idempotency.ComputeHash(new
            {
                Kind = "poll",
                Alias = alias,
                command.Question,
                Options = command.Options.Select(o => o.Text).ToList(),
                command.AllowMultipleAnswers,
                command.IsAnonymous,
                command.Duration
            });

            var replay = await Idempotency.FindReplayAsync<Poll>(db, key, hash, ct);
            if (replay is not null)
            {
                return new(replay.ToDetails(), Replayed: true);
            }
        }

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
            Options = command.Options.ToEntities(),
            IdempotencyKey = command.IdempotencyKey,
            IdempotencyHash = hash
        };

        db.Polls.Add(poll);
        if (await Idempotency.SaveNewAsync(poll, db, ct) is { } winner)
        {
            return new(winner.ToDetails(), Replayed: true);
        }

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

        return new(poll.ToDetails(), Replayed: false);
    }
}
