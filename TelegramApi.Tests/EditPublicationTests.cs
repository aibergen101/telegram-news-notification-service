using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.EditPublication;
using TelegramApi.Domain;
using TelegramApi.Tests.Support;

namespace TelegramApi.Tests;

public sealed class EditPublicationTests : IDisposable
{
    private readonly TestDatabase database = new();
    private readonly FakeTelegramService telegram = new();

    public void Dispose() => database.Dispose();

    [Fact]
    public async Task TextPublication_ReplacesTextAndButtons()
    {
        var id = await SeedAsync(withMedia: false, buttons: 2);

        var result = await EditAsync(new EditPublicationCommand(
            id, "New text", TextFormat.Html, [new ButtonDto("Read", "https://example.com/read")]));

        Assert.Equal(["EditText:42:New text:1"], telegram.Calls);
        Assert.Equal("New text", result.Text);
        Assert.Equal(TextFormat.Html, result.Format);
        Assert.Equal("Read", Assert.Single(result.Buttons).Text);
        Assert.NotNull(result.EditedAt);

        await using var db = database.CreateContext();
        var saved = await db.Publications.Include(p => p.Buttons).SingleAsync();
        Assert.Equal("New text", saved.Text);
        Assert.Single(saved.Buttons);
        Assert.Equal(1, await db.Set<Button>().CountAsync());
    }

    [Fact]
    public async Task TextPublication_RequiresText()
    {
        var id = await SeedAsync(withMedia: false);

        await Assert.ThrowsAsync<ValidationException>(() =>
            EditAsync(new EditPublicationCommand(id, null, TextFormat.Plain, [])));
        Assert.Empty(telegram.Calls);
    }

    [Fact]
    public async Task MediaPublication_EditsCaptionOfFirstMessage()
    {
        var id = await SeedAsync(withMedia: true);

        var result = await EditAsync(new EditPublicationCommand(id, "New caption", TextFormat.Plain, []));

        Assert.Equal(["EditCaption:42:New caption"], telegram.Calls);
        Assert.Equal("New caption", result.Text);
        Assert.Equal(2, result.MediaFiles.Count);
    }

    [Fact]
    public async Task MediaPublication_CaptionCanBeRemoved()
    {
        var id = await SeedAsync(withMedia: true);

        var result = await EditAsync(new EditPublicationCommand(id, null, TextFormat.Plain, []));

        Assert.Equal(["EditCaption:42:"], telegram.Calls);
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task MediaPublication_RejectsButtonsAndLongCaption()
    {
        var id = await SeedAsync(withMedia: true);

        var exception = await Assert.ThrowsAsync<ValidationException>(() => EditAsync(new EditPublicationCommand(
            id,
            new string('a', TelegramLimits.MaxCaptionLength + 1),
            TextFormat.Plain,
            [new ButtonDto("Read", "https://example.com")])));

        Assert.Equal(2, exception.Errors.Count());
        Assert.Empty(telegram.Calls);
    }

    [Theory]
    [InlineData(PublicationStatus.Failed)]
    [InlineData(PublicationStatus.Deleted)]
    public async Task NotSentPublication_Conflicts(PublicationStatus status)
    {
        var id = await SeedAsync(withMedia: false, status: status);

        await Assert.ThrowsAsync<ConflictException>(() =>
            EditAsync(new EditPublicationCommand(id, "New text", TextFormat.Plain, [])));
        Assert.Empty(telegram.Calls);
    }

    [Fact]
    public async Task UnknownPublication_NotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            EditAsync(new EditPublicationCommand(Guid.NewGuid(), "New text", TextFormat.Plain, [])));
    }

    [Fact]
    public async Task TelegramError_LeavesRecordUnchanged()
    {
        var id = await SeedAsync(withMedia: false);
        telegram.FailWith = new TelegramOperationException("Bad Request: can't parse entities", 400);

        await Assert.ThrowsAsync<TelegramOperationException>(() =>
            EditAsync(new EditPublicationCommand(id, "<b>broken", TextFormat.Html, [])));

        await using var db = database.CreateContext();
        var saved = await db.Publications.SingleAsync();
        Assert.Equal("Original", saved.Text);
        Assert.Equal(PublicationStatus.Sent, saved.Status);
        Assert.Null(saved.EditedAt);
    }

    [Fact]
    public void Validator_RejectsTooLongTextAndBadButton()
    {
        var result = new EditPublicationValidator().Validate(new EditPublicationCommand(
            Guid.NewGuid(),
            new string('a', TelegramLimits.MaxMessageLength + 1),
            TextFormat.Plain,
            [new ButtonDto("Read", "http://example.com")]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Text");
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("Buttons", StringComparison.Ordinal));
    }

    private async Task<Guid> SeedAsync(
        bool withMedia,
        int buttons = 0,
        PublicationStatus status = PublicationStatus.Sent)
    {
        var publication = new Publication
        {
            Id = Guid.NewGuid(),
            TgAlias = "@test_channel",
            TgChatId = FakeTelegramService.ChatId,
            TgMsgId = 42,
            Status = status,
            Text = "Original",
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < buttons; i++)
        {
            publication.Buttons.Add(new Button { Id = Guid.NewGuid(), Text = $"B{i}", Url = "https://example.com", Order = i });
        }

        if (withMedia)
        {
            publication.MediaFiles.Add(new MediaFile { FileName = "a.png", TgFileId = "f1", TgMessageId = 42, Order = 0 });
            publication.MediaFiles.Add(new MediaFile { FileName = "b.png", TgFileId = "f2", TgMessageId = 43, Order = 1 });
        }

        await using var db = database.CreateContext();
        db.Publications.Add(publication);
        await db.SaveChangesAsync();
        return publication.Id;
    }

    private async Task<PublicationDetails> EditAsync(EditPublicationCommand command)
    {
        await using var db = database.CreateContext();
        return await EditPublicationHandler.Handle(
            command, db, telegram, NullLogger<EditPublicationHandler>.Instance, CancellationToken.None);
    }
}
