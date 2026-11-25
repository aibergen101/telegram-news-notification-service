using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Common;

public static class TestData
{
    public const string Alias = "@test_channel";
    public const long ChatId = -100123456789;

    public static MediaFileDto File(
        string contentType = "image/jpeg",
        long length = 1024,
        string fileName = "photo.jpg") =>
        new(fileName, new MemoryStream([1, 2, 3]), contentType, length);

    public static Publication SentPublication(DateTime createdAt, string alias = Alias, string text = "Hello") =>
        new()
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            TgChatId = ChatId,
            TgMsgId = 10,
            Status = PublicationStatus.Sent,
            Text = text,
            CreatedAt = createdAt,
            SentAt = createdAt
        };

    public static Poll SentPoll(DateTime createdAt, string alias = Alias, string question = "Question?") =>
        new()
        {
            Id = Guid.NewGuid(),
            TgAlias = alias,
            TgChatId = ChatId,
            TgMsgId = 20,
            Status = PublicationStatus.Sent,
            Question = question,
            CreatedAt = createdAt,
            SentAt = createdAt,
            Options =
            [
                new PollOption { Id = Guid.NewGuid(), Text = "Yes", Order = 0 },
                new PollOption { Id = Guid.NewGuid(), Text = "No", Order = 1 }
            ]
        };
}
