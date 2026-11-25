using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Domain;
using TelegramApi.Tests.Support;

namespace TelegramApi.Tests;

public sealed class IdempotencyTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly FakeTelegramService telegram = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task CreateText_WithoutKey_PublishesEveryTime()
    {
        var command = TextCommand("Hello", key: null);

        var first = await CreateTextAsync(command);
        var second = await CreateTextAsync(command);

        Assert.False(first.IsReplay);
        Assert.False(second.IsReplay);
        Assert.NotEqual(first.Details.Id, second.Details.Id);
        Assert.Equal(2, telegram.Calls.Count);
    }

    [Fact]
    public async Task CreateText_SameKeyAndContent_ReturnsExistingWithoutSecondPublication()
    {
        var first = await CreateTextAsync(TextCommand("Hello", "key-1"));
        var second = await CreateTextAsync(TextCommand("Hello", "key-1"));

        Assert.False(first.IsReplay);
        Assert.True(second.IsReplay);
        Assert.Equal(first.Details.Id, second.Details.Id);
        Assert.Equal(first.Details.TgMessageId, second.Details.TgMessageId);
        Assert.Single(telegram.Calls);

        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Publications.CountAsync());
    }

    [Fact]
    public async Task CreateText_AliasWithoutAt_IsTheSameRequest()
    {
        await CreateTextAsync(TextCommand("Hello", "key-1") with { ChannelAlias = "@test_channel" });
        var second = await CreateTextAsync(TextCommand("Hello", "key-1") with { ChannelAlias = "test_channel" });

        Assert.True(second.IsReplay);
        Assert.Single(telegram.Calls);
    }

    [Fact]
    public async Task CreateText_SameKeyDifferentContent_Conflicts()
    {
        await CreateTextAsync(TextCommand("Hello", "key-1"));

        await Assert.ThrowsAsync<ConflictException>(() => CreateTextAsync(TextCommand("Other text", "key-1")));
        await Assert.ThrowsAsync<ConflictException>(() => CreateTextAsync(
            TextCommand("Hello", "key-1") with { Buttons = [new ButtonDto("Open", "https://example.com")] }));

        Assert.Single(telegram.Calls);
    }

    [Fact]
    public async Task CreateText_KeyOfMediaPublication_Conflicts()
    {
        await CreateMediaAsync(MediaCommand("key-1", [1, 2, 3]));

        await Assert.ThrowsAsync<ConflictException>(() => CreateTextAsync(TextCommand("Hello", "key-1")));
    }

    [Fact]
    public async Task CreateText_FailedPublicationIsReturnedOnRetryWithoutResending()
    {
        telegram.FailWith = new TelegramOperationException("Bad Request: chat not found", 400);
        await Assert.ThrowsAsync<TelegramOperationException>(() => CreateTextAsync(TextCommand("Hello", "key-1")));

        telegram.FailWith = null;
        var retry = await CreateTextAsync(TextCommand("Hello", "key-1"));

        Assert.True(retry.IsReplay);
        Assert.Equal(PublicationStatus.Failed, retry.Details.Status);
        Assert.Empty(telegram.Calls);
    }

    [Fact]
    public async Task CreateMedia_SameKeyAndFiles_ReturnsExisting_AndSendsFullContent()
    {
        var first = await CreateMediaAsync(MediaCommand("key-1", [1, 2, 3]));
        var second = await CreateMediaAsync(MediaCommand("key-1", [1, 2, 3]));

        Assert.True(second.IsReplay);
        Assert.Equal(first.Details.Id, second.Details.Id);
        Assert.Single(telegram.Calls);
        // Хеширование не должно «съесть» поток: в Telegram уходит весь файл.
        Assert.Equal([1, 2, 3], Assert.Single(telegram.SentFileContents));
    }

    [Fact]
    public async Task CreateMedia_SameKeyDifferentFileContent_Conflicts()
    {
        await CreateMediaAsync(MediaCommand("key-1", [1, 2, 3]));

        await Assert.ThrowsAsync<ConflictException>(() => CreateMediaAsync(MediaCommand("key-1", [1, 2, 4])));
        Assert.Single(telegram.Calls);
    }

    [Fact]
    public async Task CreatePoll_SameKeyAndContent_ReturnsExisting()
    {
        var first = await CreatePollAsync(PollCommand("Question?", "key-1"));
        var second = await CreatePollAsync(PollCommand("Question?", "key-1"));

        Assert.True(second.IsReplay);
        Assert.Equal(first.Details.Id, second.Details.Id);
        Assert.Equal(2, second.Details.Options.Count);
        Assert.Single(telegram.Calls);

        await Assert.ThrowsAsync<ConflictException>(() => CreatePollAsync(PollCommand("Another?", "key-1")));
    }

    [Fact]
    public async Task Database_RejectsDuplicateKey()
    {
        await using var db = database.CreateContext();
        db.Publications.Add(NewPublication("key-1"));
        db.Publications.Add(NewPublication("key-1"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_AllowsManyRecordsWithoutKey()
    {
        await using var db = database.CreateContext();
        db.Publications.Add(NewPublication(null));
        db.Publications.Add(NewPublication(null));

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.Publications.CountAsync());
    }

    [Fact]
    public async Task SaveNew_WhenConcurrentRequestSavedSameKey_ReturnsThatRecord()
    {
        // Параллельный запрос успел сохранить запись после нашей проверки, но до нашей вставки.
        var winner = NewPublication("key-1");
        await using (var other = database.CreateContext())
        {
            other.Publications.Add(winner);
            await other.SaveChangesAsync();
        }

        await using var db = database.CreateContext();
        var loser = NewPublication("key-1");
        loser.Buttons.Add(new Button { Id = Guid.NewGuid(), Text = "Open", Url = "https://example.com" });

        var existing = await Idempotency.SaveNewAsync(db, db.Publications, db.Publications, loser, CancellationToken.None);

        Assert.NotNull(existing);
        Assert.Equal(winner.Id, existing.Id);
        Assert.Equal(EntityState.Detached, db.Entry(loser).State);
        Assert.Equal(1, await db.Publications.CountAsync());
    }

    [Fact]
    public async Task SaveNew_WhenConcurrentRequestUsedKeyWithOtherContent_Conflicts()
    {
        var winner = NewPublication("key-1");
        winner.RequestHash = "other";
        await using (var other = database.CreateContext())
        {
            other.Publications.Add(winner);
            await other.SaveChangesAsync();
        }

        await using var db = database.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            Idempotency.SaveNewAsync(db, db.Publications, db.Publications, NewPublication("key-1"), CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validator_RejectsBlankKey(string key)
    {
        var result = new CreateTextPublicationValidator().TestValidate(TextCommand("Hello", key));

        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    [Fact]
    public void Validator_RejectsTooLongKey()
    {
        var result = new CreateQuestionPollValidator().TestValidate(
            PollCommand("Question?", new string('k', Idempotency.MaxKeyLength + 1)));

        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    [Fact]
    public void Validator_AcceptsMissingOrNormalKey()
    {
        var validator = new CreateTextPublicationValidator();

        validator.TestValidate(TextCommand("Hello", null)).ShouldNotHaveValidationErrorFor(x => x.IdempotencyKey);
        validator.TestValidate(TextCommand("Hello", Guid.NewGuid().ToString()))
            .ShouldNotHaveValidationErrorFor(x => x.IdempotencyKey);
    }

    private static CreateTextPublicationCommand TextCommand(string text, string? key) =>
        new("@test_channel", text, TextFormat.Plain, [], key);

    private static CreateMediaPublicationCommand MediaCommand(string? key, byte[] content) =>
        new("@test_channel", "Caption", TextFormat.Plain,
            [new MediaFileDto("photo.png", new MemoryStream(content), "image/png", content.Length)], key);

    private static CreateQuestionPollCommand PollCommand(string question, string? key) =>
        new("@test_channel", question, [new PollOptionDto("Yes"), new PollOptionDto("No")], false, true, null, key);

    private static Publication NewPublication(string? key) => new()
    {
        Id = Guid.NewGuid(),
        TgAlias = "@test_channel",
        Text = "Hello",
        CreatedAt = DateTime.UtcNow,
        IdempotencyKey = key,
        RequestHash = key is null ? null : "hash"
    };

    private async Task<CreateResult<PublicationDetails>> CreateTextAsync(CreateTextPublicationCommand command)
    {
        await using var db = database.CreateContext();
        return await CreateTextPublicationHandler.Handle(
            command, db, telegram, NullLogger<CreateTextPublicationHandler>.Instance, CancellationToken.None);
    }

    private async Task<CreateResult<PublicationDetails>> CreateMediaAsync(CreateMediaPublicationCommand command)
    {
        await using var db = database.CreateContext();
        return await CreateMediaPublicationHandler.Handle(
            command, db, telegram, NullLogger<CreateMediaPublicationHandler>.Instance, CancellationToken.None);
    }

    private async Task<CreateResult<PollDetails>> CreatePollAsync(CreateQuestionPollCommand command)
    {
        await using var db = database.CreateContext();
        return await CreateQuestionPollHandler.Handle(
            command, db, telegram, NullLogger<CreateQuestionPollHandler>.Instance, CancellationToken.None);
    }
}
