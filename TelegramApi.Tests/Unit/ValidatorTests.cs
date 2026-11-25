using TelegramApi.Application.Common;
using TelegramApi.Application.History.GetHistory;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Application.Publications.EditPublication;
using TelegramApi.Domain;

namespace TelegramApi.Tests.Unit;

public sealed class ValidatorTests
{
    private static readonly ButtonDto ValidButton = new("Open", "https://example.com");

    [Theory]
    [InlineData("@my_channel", true)]
    [InlineData("my_channel", true)]
    [InlineData("@abc", false)]
    [InlineData("@1channel", false)]
    [InlineData("@bad-name", false)]
    [InlineData("", false)]
    public void TextPublication_ChannelAlias(string alias, bool valid)
    {
        var result = new CreateTextPublicationValidator().Validate(new CreateTextPublicationCommand(alias, "text", TextFormat.Plain, []));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void TextPublication_TextLimits()
    {
        var validator = new CreateTextPublicationValidator();

        Assert.False(validator.Validate(new CreateTextPublicationCommand("@my_channel", "", TextFormat.Plain, [])).IsValid);
        Assert.True(validator.Validate(new CreateTextPublicationCommand("@my_channel", new string('a', 4096), TextFormat.Plain, [])).IsValid);
        Assert.False(validator.Validate(new CreateTextPublicationCommand("@my_channel", new string('a', 4097), TextFormat.Plain, [])).IsValid);
    }

    [Theory]
    [InlineData("https://example.com/path?q=1", true)]
    [InlineData("http://example.com", false)]
    [InlineData("ftp://example.com", false)]
    [InlineData("/relative", false)]
    [InlineData("javascript:alert(1)", false)]
    public void Button_OnlyAbsoluteHttps(string url, bool valid)
    {
        Assert.Equal(valid, new ButtonDtoValidator().Validate(new ButtonDto("Open", url)).IsValid);
    }

    [Fact]
    public void TextPublication_ButtonCountLimit()
    {
        var validator = new CreateTextPublicationValidator();
        var max = Enumerable.Repeat(ValidButton, TelegramLimits.MaxButtons).ToList();
        var tooMany = Enumerable.Repeat(ValidButton, TelegramLimits.MaxButtons + 1).ToList();

        Assert.True(validator.Validate(new CreateTextPublicationCommand("@my_channel", "t", TextFormat.Plain, max)).IsValid);
        Assert.False(validator.Validate(new CreateTextPublicationCommand("@my_channel", "t", TextFormat.Plain, tooMany)).IsValid);
    }

    [Fact]
    public void TextPublication_UnknownFormat_Invalid()
    {
        var result = new CreateTextPublicationValidator().Validate(
            new CreateTextPublicationCommand("@my_channel", "t", (TextFormat)42, []));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("order-42", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    public void IdempotencyKey_Format(string? key, bool valid)
    {
        var result = new CreateTextPublicationValidator().Validate(
            new CreateTextPublicationCommand("@my_channel", "t", TextFormat.Plain, [], key));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Media_RequiresFiles_AndLimitsAlbumSize()
    {
        var validator = new CreateMediaPublicationValidator();

        Assert.False(validator.Validate(Media([])).IsValid);
        Assert.True(validator.Validate(Media(Files(10, "image/jpeg"))).IsValid);
        Assert.False(validator.Validate(Media(Files(11, "image/jpeg"))).IsValid);
    }

    [Fact]
    public void Media_CannotMixPhotosAndDocuments()
    {
        var files = Files(1, "image/png").Concat(Files(1, "application/pdf")).ToList();

        Assert.False(new CreateMediaPublicationValidator().Validate(Media(files)).IsValid);
    }

    [Fact]
    public void Media_ButtonsOnlyForSingleFile()
    {
        var validator = new CreateMediaPublicationValidator();

        Assert.True(validator.Validate(Media(Files(1, "image/png"), [ValidButton])).IsValid);
        Assert.False(validator.Validate(Media(Files(2, "image/png"), [ValidButton])).IsValid);
    }

    [Fact]
    public void Media_SizeAndTypeLimits()
    {
        var validator = new MediaFileDtoValidator();

        Assert.False(validator.Validate(File("image/png", TelegramLimits.MaxPhotoBytes + 1)).IsValid);
        Assert.True(validator.Validate(File("application/pdf", TelegramLimits.MaxPhotoBytes + 1)).IsValid);
        Assert.False(validator.Validate(File("application/pdf", TelegramLimits.MaxDocumentBytes + 1)).IsValid);
        Assert.False(validator.Validate(File("application/x-msdownload", 10)).IsValid);
        Assert.False(validator.Validate(File("image/png", 0)).IsValid);
    }

    [Fact]
    public void Media_CaptionLimit()
    {
        var validator = new CreateMediaPublicationValidator();
        var files = Files(1, "image/png");

        Assert.True(validator.Validate(Media(files) with { Text = new string('a', 1024) }).IsValid);
        Assert.False(validator.Validate(Media(files) with { Text = new string('a', 1025) }).IsValid);
    }

    [Theory]
    [InlineData(2, null, true)]
    [InlineData(10, 600, true)]
    [InlineData(1, null, false)]
    [InlineData(11, null, false)]
    [InlineData(2, 4, false)]
    [InlineData(2, 601, false)]
    public void Poll_OptionsAndDuration(int options, int? duration, bool valid)
    {
        var command = new CreateQuestionPollCommand(
            "@my_channel",
            "Question?",
            Enumerable.Range(0, options).Select(i => new PollOptionDto($"Option {i}")).ToList(),
            AllowMultipleAnswers: false,
            IsAnonymous: true,
            duration);

        Assert.Equal(valid, new CreateQuestionPollValidator().Validate(command).IsValid);
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void History_Paging(int page, int pageSize, bool valid)
    {
        var result = new GetHistoryValidator().Validate(new GetHistoryQuery(page, pageSize, null, null, null, null));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void History_ToBeforeFrom_Invalid()
    {
        var query = new GetHistoryQuery(1, 20, null, null, new DateTime(2026, 2, 1), new DateTime(2026, 1, 1));

        Assert.False(new GetHistoryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Edit_ButtonUrlMustBeHttps()
    {
        var command = new EditPublicationCommand(Guid.NewGuid(), "t", TextFormat.Plain, [new ButtonDto("x", "http://a.b")]);

        Assert.False(new EditPublicationValidator().Validate(command).IsValid);
    }

    private static CreateMediaPublicationCommand Media(List<MediaFileDto> files, List<ButtonDto>? buttons = null) =>
        new("@my_channel", null, TextFormat.Plain, files, buttons ?? []);

    private static List<MediaFileDto> Files(int count, string contentType) =>
        Enumerable.Range(0, count).Select(i => File(contentType, 100, $"f{i}")).ToList();

    private static MediaFileDto File(string contentType, long length, string name = "f") =>
        new(name, Stream.Null, contentType, length);
}
