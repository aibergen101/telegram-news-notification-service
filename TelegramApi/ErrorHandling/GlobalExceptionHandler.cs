using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TelegramApi.Application.Common;

namespace TelegramApi.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = CreateProblem(exception);
        Log(exception, problem.Status ?? StatusCodes.Status500InternalServerError);

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails CreateProblem(Exception exception)
    {
        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                return new HttpValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed"
                };

            case BadHttpRequestException badRequest:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad request",
                    Detail = badRequest.Message
                };

            case NotFoundException or ChannelNotFoundException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not found",
                    Detail = exception.Message
                };

            case BotLacksRightsException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "The bot lacks the required rights in this channel",
                    Detail = exception.Message
                };

            case ConflictException:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Operation is not possible in the current state",
                    Detail = exception.Message
                };

            case TelegramOperationException telegram:
                return CreateTelegramProblem(telegram);

            default:
                return new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal server error"
                };
        }
    }

    private static ProblemDetails CreateTelegramProblem(TelegramOperationException exception)
    {
        var status = exception.IsTimeout ? StatusCodes.Status504GatewayTimeout
            : exception.ErrorCode == 400 ? StatusCodes.Status422UnprocessableEntity
            : StatusCodes.Status502BadGateway;

        return new ProblemDetails
        {
            Status = status,
            Title = "Telegram did not complete the request",
            Detail = exception.Message
        };
    }

    private void Log(Exception exception, int status)
    {
        if (status >= StatusCodes.Status500InternalServerError && exception is not TelegramOperationException)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogWarning("Request failed with {StatusCode}: {ExceptionType}", status, exception.GetType().Name);
        }
    }
}
