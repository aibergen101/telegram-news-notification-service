using FluentValidation;
using Microsoft.EntityFrameworkCore;
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

        RuleFor(x => x.IdempotencyKey).MustBeIdempotencyKey();
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
        var alias = ChannelAlias.Normalize(command.ChannelAlias);
        var requestHash = command.IdempotencyKey is null ? null : ComputeRequestHash(command, alias);
        var lookup = db.Polls.Include(p => p.Options);

        var existing = await Idempotency.FindExistingAsync(lookup, command.IdempotencyKey, requestHash, ct);
        if (existing is not null)
        {
            return new CreateResult<PollDetails>(existing.ToDetails(), IsReplay: true);
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
            RequestHash = requestHash
        };

        var concurrent = await Idempotency.SaveNewAsync(db, db.Polls, lookup, poll, ct);
        if (concurrent is not null)
        {
            return new CreateResult<PollDetails>(concurrent.ToDetails(), IsReplay: true);
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

        return new CreateResult<PollDetails>(poll.ToDetails(), IsReplay: false);
    }

    private static string ComputeRequestHash(CreateQuestionPollCommand command, string alias) =>
        Idempotency.ComputeHash(new
        {
            Kind = "poll",
            ChannelAlias = alias,
            command.Question,
            command.Options,
            command.AllowMultipleAnswers,
            command.IsAnonymous,
            command.Duration
        });
}
