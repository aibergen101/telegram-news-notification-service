using TelegramApi.Domain;
using TelegramApi.Application.Common;

namespace TelegramApi.Application.Publications.CreatePoll;

public class CreateQuestionPollHandler
{
    public static async Task<Guid> Handle(CreateQuestionPollCommand cmd, IApplicationDbContext db, ITelegramService tg)
    {
        if (!await tg.CanPublishToChannelAsync(cmd.ChannelAlias))
            throw new InvalidOperationException($"Channel '{cmd.ChannelAlias}' is not accessible or bot lacks required permissions");

        Poll poll = new Poll
        {
            Id = Guid.NewGuid(),
            TgMsgId = 0,
            TgChatId = 0,
            TgAlias = cmd.ChannelAlias,
            CreatedAt = DateTime.UtcNow,
            Question = cmd.Question,
            Duration = cmd.Duration,
            IsClosed = false,
            AllowMultipleAnswers = cmd.AllowMultipleAnswers,
            IsAnonymous = cmd.IsAnonymous,
            Status = PublicationStatus.Failed
        };

        poll.Options = cmd.Options.Select((o, index) => new PollOption
        {
            Id = Guid.NewGuid(),
            Text = o.Text,
            Order = index,
            PollId = poll.Id
        }).ToList();

        db.Polls.Add(poll);
        await db.SaveChangesAsync();

        try
        {
            var (messageId, chatId) = await tg.SendPollAsync(
                cmd.ChannelAlias,
                cmd.Question,
                cmd.Options.Select(o => o.Text).ToList(),
                cmd.AllowMultipleAnswers,
                cmd.IsAnonymous,
                cmd.Duration);

            poll.TgMsgId = messageId;
            poll.TgChatId = chatId;
            poll.Status = PublicationStatus.Sent;
        }
        catch (Exception ex)
        {
            poll.Status = PublicationStatus.Failed;
        }

        await db.SaveChangesAsync();
        return poll.Id;
    }
}