using Microsoft.AspNetCore.Mvc;
using TelegramApi.Application.Common;
using TelegramApi.Application.History.GetHistory;
using TelegramApi.Application.Polls.ClosePoll;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Application.Polls.DeletePoll;
using TelegramApi.Application.Polls.GetPoll;
using TelegramApi.Application.Polls.PinPoll;
using TelegramApi.Application.Polls.UnpinPoll;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Application.Publications.DeletePublication;
using TelegramApi.Application.Publications.EditPublication;
using TelegramApi.Application.Publications.GetPublication;
using TelegramApi.Application.Publications.PinPublication;
using TelegramApi.Application.Publications.UnpinPublication;
using TelegramApi.Application.TelegramInfo.CheckBot;
using TelegramApi.Application.TelegramInfo.GetChannelInfo;
using TelegramApi.Contracts;
using TelegramApi.Domain;
using TelegramApi.Mapping;
using Wolverine;

namespace TelegramApi.Endpoints;

public static class PublicationEndpoints
{
    public static IEndpointRouteBuilder MapPublicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/publications").WithTags("Publications");

        group.MapPost("/", CreateTextAsync)
            .WithName("CreateTextPublication")
            .WithSummary("Создать текстовую публикацию")
            .WithDescription("Отправляет текст в канал. Запись сохраняется до отправки: если Telegram откажет, она останется со статусом Failed. " + EndpointMetadataExtensions.IdempotencyDescription)
            .Produces<PublicationResponse>(StatusCodes.Status201Created)
            .Produces<PublicationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        app.MapPost("/api/media-publications", CreateMediaAsync)
            .WithTags("Publications")
            .WithName("CreateMediaPublication")
            .WithSummary("Создать публикацию с файлами")
            .WithDescription("Одно изображение или документ, либо альбом из 2–10 файлов одного вида. " + EndpointMetadataExtensions.IdempotencyDescription)
            .DisableAntiforgery()
            .Produces<PublicationResponse>(StatusCodes.Status201Created)
            .Produces<PublicationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetPublication")
            .WithSummary("Получить публикацию по идентификатору")
            .Produces<PublicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", EditAsync)
            .WithName("EditPublication")
            .WithSummary("Отредактировать публикацию")
            .WithDescription("Заменяет содержимое опубликованного сообщения. Текстовая публикация: текст (1–4096), форматирование и кнопки. "
                + "Медиа-публикация: только подпись (до 1024) и форматирование; сами файлы и кнопки изменить нельзя, у альбома меняется подпись первого сообщения. "
                + "Редактировать можно только публикацию со статусом Sent. Опросы Telegram редактировать не позволяет.")
            .Produces<PublicationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapPut("/{id:guid}/pin", PinAsync)
            .WithName("PinPublication")
            .WithSummary("Закрепить публикацию в канале")
            .WithDescription(EndpointMetadataExtensions.PinDescription)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}/pin", UnpinAsync)
            .WithName("UnpinPublication")
            .WithSummary("Открепить публикацию в канале")
            .WithDescription(EndpointMetadataExtensions.UnpinDescription)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePublication")
            .WithSummary("Удалить публикацию")
            .WithDescription("Удаляет сообщения в Telegram (для альбома все) и ставит статус Deleted. Повторное удаление безопасно.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesTelegramProblems();

        return app;
    }

    private static async Task<IResult> CreateTextAsync(
        CreateTextPublicationRequest request,
        [FromHeader(Name = Idempotency.HeaderName)] string? idempotencyKey,
        HttpResponse response,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PublicationDetails>>(request.ToCommand(idempotencyKey), ct);
        return IdempotentResults.Created(
            response, result.IsReplay, $"/api/publications/{result.Details.Id}", result.Details.ToResponse());
    }

    private static async Task<IResult> CreateMediaAsync(
        [FromForm] CreateMediaPublicationRequest request,
        [FromHeader(Name = Idempotency.HeaderName)] string? idempotencyKey,
        HttpResponse response,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PublicationDetails>>(request.ToCommand(idempotencyKey), ct);
        return IdempotentResults.Created(
            response, result.IsReplay, $"/api/publications/{result.Details.Id}", result.Details.ToResponse());
    }

    private static async Task<IResult> EditAsync(
        Guid id,
        EditPublicationRequest request,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PublicationDetails>(request.ToCommand(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> PinAsync(
        Guid id,
        IMessageBus bus,
        CancellationToken ct,
        bool disableNotification = false)
    {
        await bus.InvokeAsync(new PinPublicationCommand(id, disableNotification), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> UnpinAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new UnpinPublicationCommand(id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PublicationDetails>(new GetPublicationQuery(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePublicationCommand(id), ct);
        return TypedResults.NoContent();
    }
}

public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/polls").WithTags("Polls");

        group.MapPost("/", CreateAsync)
            .WithName("CreatePoll")
            .WithSummary("Создать опрос")
            .WithDescription("Публикует опрос в канале. Продолжительность в секундах (5–600); без неё опрос не ограничен по времени. " + EndpointMetadataExtensions.IdempotencyDescription)
            .Produces<PollResponse>(StatusCodes.Status201Created)
            .Produces<PollResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetPoll")
            .WithSummary("Получить опрос по идентификатору")
            .Produces<PollResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/close", CloseAsync)
            .WithName("ClosePoll")
            .WithSummary("Закрыть опрос")
            .WithDescription("Останавливает опрос в Telegram. Повторное закрытие безопасно. Закрыть можно только опрос со статусом Sent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapPut("/{id:guid}/pin", PinAsync)
            .WithName("PinPoll")
            .WithSummary("Закрепить опрос в канале")
            .WithDescription(EndpointMetadataExtensions.PinDescription)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}/pin", UnpinAsync)
            .WithName("UnpinPoll")
            .WithSummary("Открепить опрос в канале")
            .WithDescription(EndpointMetadataExtensions.UnpinDescription)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePoll")
            .WithSummary("Удалить опрос")
            .WithDescription("Удаляет сообщение с опросом в Telegram и ставит статус Deleted. Повторное удаление безопасно.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesTelegramProblems();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreatePollRequest request,
        [FromHeader(Name = Idempotency.HeaderName)] string? idempotencyKey,
        HttpResponse response,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PollDetails>>(request.ToCommand(idempotencyKey), ct);
        return IdempotentResults.Created(
            response, result.IsReplay, $"/api/polls/{result.Details.Id}", result.Details.ToResponse());
    }

    private static async Task<IResult> PinAsync(
        Guid id,
        IMessageBus bus,
        CancellationToken ct,
        bool disableNotification = false)
    {
        await bus.InvokeAsync(new PinPollCommand(id, disableNotification), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> UnpinAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new UnpinPollCommand(id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PollDetails>(new GetPollQuery(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> CloseAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new ClosePollCommand(id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePollCommand(id), ct);
        return TypedResults.NoContent();
    }
}

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/publications-history", GetAsync)
            .WithTags("History")
            .WithName("GetPublicationsHistory")
            .WithSummary("История публикаций и опросов")
            .WithDescription("Единая история, от новых к старым. Фильтры: статус, канал, период по дате создания. Страница начинается с 1, размер страницы до 100.")
            .Produces<PagedResponse<HistoryItemResponse>>()
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GetAsync(
        IMessageBus bus,
        CancellationToken ct,
        int page = 1,
        int pageSize = HistoryLimits.DefaultPageSize,
        PublicationStatus? status = null,
        string? channelAlias = null,
        [FromQuery(Name = "from")] DateTimeOffset? createdFrom = null,
        [FromQuery(Name = "to")] DateTimeOffset? createdTo = null)
    {
        var query = new GetHistoryQuery(
            page,
            pageSize,
            status,
            channelAlias,
            createdFrom?.UtcDateTime,
            createdTo?.UtcDateTime);

        var result = await bus.InvokeAsync<PagedResult<HistoryItemResult>>(query, ct);
        return TypedResults.Ok(result.ToResponse());
    }
}

public static class TelegramEndpoints
{
    public static IEndpointRouteBuilder MapTelegramEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Telegram");

        group.MapGet("/bot/check", CheckBotAsync)
            .WithName("CheckBot")
            .WithSummary("Проверить токен и доступность бота")
            .Produces<BotCheckResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapGet("/channels/{alias}", GetChannelAsync)
            .WithName("GetChannelInfo")
            .WithSummary("Информация о канале")
            .WithDescription("Alias можно указывать с @ или без.")
            .Produces<ChannelInfoResponse>()
            .ProducesValidationProblem()
            .ProducesTelegramProblems();

        return app;
    }

    private static async Task<IResult> CheckBotAsync(IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new CheckBotQuery(), ct);
        return TypedResults.Ok(new BotCheckResponse("ok"));
    }

    private static async Task<IResult> GetChannelAsync(string alias, IMessageBus bus, CancellationToken ct)
    {
        var info = await bus.InvokeAsync<ChannelInfo>(new GetChannelInfoQuery(alias), ct);
        return TypedResults.Ok(info.ToResponse());
    }
}

internal static class EndpointMetadataExtensions
{
    public const string IdempotencyDescription =
        "Необязательный заголовок Idempotency-Key (до 255 символов): повтор того же запроса с тем же ключом не публикует сообщение заново, "
        + "а возвращает уже созданную запись (200 OK, заголовок Idempotent-Replayed: true), в том числе со статусом Failed. "
        + "Тот же ключ с другим содержимым запроса даёт 409 Conflict.";

    public const string PinDescription =
        "Закрепляет сообщение в канале (у альбома первое сообщение). Параметр disableNotification=true закрепляет без уведомления подписчиков. "
        + "Боту нужно право редактирования сообщений в канале. Доступно только для записи со статусом Sent; повторный вызов безопасен.";

    public const string UnpinDescription =
        "Открепляет сообщение в канале. Доступно только для записи со статусом Sent; повторный вызов безопасен.";

    /// <summary>Ошибки, которыми отвечает любой эндпоинт, обращающийся к Telegram.</summary>
    public static RouteHandlerBuilder ProducesTelegramProblems(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}

internal static class IdempotentResults
{
    public const string ReplayedHeaderName = "Idempotent-Replayed";

    /// <summary>Новая запись: 201 Created. Повтор по ключу идемпотентности: 200 OK с той же записью.</summary>
    public static IResult Created<T>(HttpResponse response, bool isReplay, string location, T body)
    {
        if (!isReplay)
        {
            return TypedResults.Created(location, body);
        }

        response.Headers[ReplayedHeaderName] = "true";
        response.Headers.Location = location;
        return TypedResults.Ok(body);
    }
}
