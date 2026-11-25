using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.GetPoll;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Polls;

public sealed class GetPollHandlerTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Returns_poll_with_ordered_options()
    {
        var poll = TestData.SentPoll(DateTime.UtcNow);
        poll.Options.Reverse();

        await using (var seed = _database.CreateContext())
        {
            seed.Polls.Add(poll);
            await seed.SaveChangesAsync();
        }

        await using var db = _database.CreateContext();
        var result = await GetPollHandler.Handle(new GetPollQuery(poll.Id), db, CancellationToken.None);

        Assert.Equal(poll.Id, result.Id);
        Assert.Equal("Question?", result.Question);
        Assert.Equal(PublicationStatus.Sent, result.Status);
        Assert.Equal(["Yes", "No"], result.Options.Select(o => o.Text));
        Assert.Equal([0, 1], result.Options.Select(o => o.Order));
    }

    [Fact]
    public async Task Throws_not_found_for_unknown_id()
    {
        await using var db = _database.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            GetPollHandler.Handle(new GetPollQuery(Guid.NewGuid()), db, CancellationToken.None));
    }
}
