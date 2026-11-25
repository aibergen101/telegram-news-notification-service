using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Domain;
using TelegramApi.Tests.Common;

namespace TelegramApi.Tests.Validators;

public class CreateMediaPublicationValidatorTests
{
    private const long Megabyte = 1024 * 1024;

    private readonly CreateMediaPublicationValidator _validator = new();

    private static CreateMediaPublicationCommand Command(
        IReadOnlyList<MediaFileDto> files,
        string? text = null,
        string alias = "@my_channel") =>
        new(alias, text, TextFormat.Plain, files);

    [Fact]
    public void Accepts_single_photo_without_caption()
    {
        Assert.True(_validator.Validate(Command([TestData.File()])).IsValid);
    }

    [Fact]
    public void Accepts_album_of_documents()
    {
        var files = new[]
        {
            TestData.File("application/pdf", fileName: "a.pdf"),
            TestData.File("text/csv", fileName: "b.csv")
        };

        Assert.True(_validator.Validate(Command(files)).IsValid);
    }

    [Fact]
    public void Rejects_empty_file_list()
    {
        var result = _validator.Validate(Command([]));

        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one file is required.");
    }

    [Fact]
    public void Rejects_more_than_album_limit()
    {
        var files = Enumerable.Range(0, TelegramLimits.MaxAlbumItems + 1).Select(_ => TestData.File()).ToList();

        var result = _validator.Validate(Command(files));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateMediaPublicationCommand.Files));
    }

    [Fact]
    public void Rejects_mixing_photos_and_documents()
    {
        var result = _validator.Validate(Command([TestData.File("image/png"), TestData.File("application/pdf")]));

        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("cannot be mixed", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_too_long_caption()
    {
        var result = _validator.Validate(Command([TestData.File()], new string('a', TelegramLimits.MaxCaptionLength + 1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateMediaPublicationCommand.Text));
    }

    [Theory]
    [InlineData("video/mp4")]
    [InlineData("application/x-msdownload")]
    [InlineData("")]
    public void Rejects_unsupported_content_type(string contentType)
    {
        var result = _validator.Validate(Command([TestData.File(contentType)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Files[0].ContentType");
    }

    [Fact]
    public void Rejects_empty_file()
    {
        var result = _validator.Validate(Command([TestData.File(length: 0)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Files[0].Length");
    }

    [Fact]
    public void Applies_photo_size_limit()
    {
        Assert.True(_validator.Validate(Command([TestData.File("image/jpeg", 10 * Megabyte)])).IsValid);
        Assert.False(_validator.Validate(Command([TestData.File("image/jpeg", (10 * Megabyte) + 1)])).IsValid);
    }

    [Fact]
    public void Applies_document_size_limit()
    {
        Assert.True(_validator.Validate(Command([TestData.File("application/pdf", 50 * Megabyte)])).IsValid);
        Assert.False(_validator.Validate(Command([TestData.File("application/pdf", (50 * Megabyte) + 1)])).IsValid);
    }

    [Fact]
    public void Rejects_too_long_file_name()
    {
        var name = new string('a', TelegramLimits.MaxFileNameLength + 1);

        var result = _validator.Validate(Command([TestData.File(fileName: name)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Files[0].FileName");
    }
}
