using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TelegramApi.Application.Common;
using TelegramApi.ErrorHandling;

namespace TelegramApi.Tests.ErrorHandling;

/// <summary>Проверяет таблицу кодов ошибок из README: исключение → HTTP-статус и Problem Details.</summary>
public class GlobalExceptionHandlerTests
{
    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Context = context;
            return ValueTask.FromResult(true);
        }
    }

    private static async Task<(HttpContext Http, ProblemDetails Problem)> Handle(Exception exception)
    {
        var service = new CapturingProblemDetailsService();
        var handler = new GlobalExceptionHandler(service, NullLogger<GlobalExceptionHandler>.Instance);
        var http = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(http, exception, CancellationToken.None);

        Assert.True(handled);
        return (http, service.Context!.ProblemDetails);
    }

    public static TheoryData<Exception, int> StatusCases => new()
    {
        { new NotFoundException("missing"), StatusCodes.Status404NotFound },
        { new ChannelNotFoundException("no channel"), StatusCodes.Status404NotFound },
        { new BotLacksRightsException("no rights"), StatusCodes.Status403Forbidden },
        { new ConflictException("conflict"), StatusCodes.Status409Conflict },
        { new BadHttpRequestException("bad body"), StatusCodes.Status400BadRequest },
        { new TelegramOperationException("can't parse entities", errorCode: 400), StatusCodes.Status422UnprocessableEntity },
        { new TelegramOperationException("Too Many Requests", errorCode: 429), StatusCodes.Status502BadGateway },
        { new TelegramOperationException("network error"), StatusCodes.Status502BadGateway },
        { new TelegramOperationException("timeout", isTimeout: true), StatusCodes.Status504GatewayTimeout },
        { new InvalidOperationException("secret details"), StatusCodes.Status500InternalServerError }
    };

    [Theory]
    [MemberData(nameof(StatusCases))]
    public async Task Maps_exception_to_status(Exception exception, int expectedStatus)
    {
        var (http, problem) = await Handle(exception);

        Assert.Equal(expectedStatus, http.Response.StatusCode);
        Assert.Equal(expectedStatus, problem.Status);
    }

    [Fact]
    public async Task Groups_validation_errors_by_property()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Text", "Text is required."),
            new ValidationFailure("Text", "Text is required."),
            new ValidationFailure("ChannelAlias", "Bad alias.")
        ]);

        var (http, problem) = await Handle(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        var validation = Assert.IsType<HttpValidationProblemDetails>(problem);
        Assert.Equal(["Text is required."], validation.Errors["Text"]);
        Assert.Equal(["Bad alias."], validation.Errors["ChannelAlias"]);
    }

    [Fact]
    public async Task Adds_telegram_error_code_and_publication_id()
    {
        var id = Guid.NewGuid();
        var exception = new TelegramOperationException("Bad Request", errorCode: 400) { RecordId = id };

        var (_, problem) = await Handle(exception);

        Assert.Equal(400, problem.Extensions["telegramErrorCode"]);
        Assert.Equal(id, problem.Extensions["publicationId"]);
    }

    [Fact]
    public async Task Hides_details_of_unexpected_errors()
    {
        var (_, problem) = await Handle(new InvalidOperationException("connection string with password"));

        Assert.Null(problem.Detail);
        Assert.Equal("Internal server error", problem.Title);
    }
}
