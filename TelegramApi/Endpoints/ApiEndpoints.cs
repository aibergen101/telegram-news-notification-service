using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using TelegramApi.Application.Common;
using TelegramApi.Application.History.GetHistory;
using TelegramApi.Application.Pinning;
using TelegramApi.Application.Polls.ClosePoll;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Application.Polls.DeletePoll;
using TelegramApi.Application.Polls.GetPoll;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Application.Publications.DeletePublication;
using TelegramApi.Application.Publications.EditPublication;
using TelegramApi.Application.Publications.GetPublication;
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
        var group = app.MapGroup("/api/publications").WithTags("Publications")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/", CreateTextAsync)
            .WithName("CreateTextPublication")
            .WithSummary("Создать текстовую публикацию")
            .WithDescription("Отправляет текст в канал. Запись сохраняется до отправки: если Telegram откажет, она останется со статусом Failed. "
                + IdempotencyDocs.Description)
            .Produces<PublicationResponse>(StatusCodes.Status201Created)
            .Produces<PublicationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        app.MapPost("/api/media-publications", CreateMediaAsync)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithTags("Publications")
            .WithName("CreateMediaPublication")
            .WithSummary("Создать публикацию с файлами")
            .WithDescription("Одно изображение или документ, либо альбом из 2–10 файлов одного вида. Кнопки допустимы только для одного файла. "
                + IdempotencyDocs.Description)
            .DisableAntiforgery()
            .Produces<PublicationResponse>(StatusCodes.Status201Created)
            .Produces<PublicationResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetPublication")
            .WithSummary("Получить публикацию по идентификатору")
            .WithDescription("Возвращает сохранённые данные публикации в любом статусе, включая Failed и Deleted.")
            .Produces<PublicationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", EditAsync)
            .WithName("EditPublication")
            .WithSummary("Редактировать публикацию")
            .WithDescription("Заменяет текст (или подпись к файлам), формат и кнопки уже отправленного сообщения. "
                + "Файлы не меняются. Редактировать можно только публикацию со статусом Sent; у альбома меняется подпись, кнопки недоступны.")
            .Produces<PublicationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePublication")
            .WithSummary("Удалить публикацию")
            .WithDescription("Удаляет сообщения в Telegram (для альбома все) и ставит статус Deleted. Повторное удаление безопасно.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesTelegramProblems();

        group.MapPinEndpoints(PublicationKind.Post, "PublicationPin");

        return app;
    }

    private static async Task<IResult> CreateTextAsync(
        CreateTextPublicationRequest request,
        [FromHeader(Name = Idempotency.HeaderName), Description("Необязательный ключ идемпотентности, до 255 видимых ASCII-символов.")] string? idempotencyKey,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PublicationDetails>>(request.ToCommand(idempotencyKey), ct);
        return CreatedOrReplayed($"/api/publications/{result.Value.Id}", result, result.Value.ToResponse());
    }

    private static async Task<IResult> CreateMediaAsync(
        [FromForm] CreateMediaPublicationRequest request,
        [FromHeader(Name = Idempotency.HeaderName), Description("Необязательный ключ идемпотентности, до 255 видимых ASCII-символов.")] string? idempotencyKey,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PublicationDetails>>(request.ToCommand(idempotencyKey), ct);
        return CreatedOrReplayed($"/api/publications/{result.Value.Id}", result, result.Value.ToResponse());
    }

    private static async Task<IResult> GetAsync([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PublicationDetails>(new GetPublicationQuery(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> EditAsync(
        [Description("Внутренний идентификатор записи (Guid).")] Guid id,
        EditPublicationRequest request,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PublicationDetails>(request.ToCommand(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> DeleteAsync([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePublicationCommand(id), ct);
        return TypedResults.NoContent();
    }

    internal static IResult CreatedOrReplayed<TDetails, TResponse>(
        string location,
        CreateResult<TDetails> result,
        TResponse response) =>
        result.Replayed
            ? TypedResults.Ok(response)
            : TypedResults.Created(location, response);
}

public static class PollEndpoints
{
    public static IEndpointRouteBuilder MapPollEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/polls").WithTags("Polls")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost("/", CreateAsync)
            .WithName("CreatePoll")
            .WithSummary("Создать опрос")
            .WithDescription("Публикует опрос в канале. Продолжительность в секундах (5–600); без неё опрос не ограничен по времени. "
                + IdempotencyDocs.Description)
            .Produces<PollResponse>(StatusCodes.Status201Created)
            .Produces<PollResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapGet("/{id:guid}", GetAsync)
            .WithName("GetPoll")
            .WithSummary("Получить опрос по идентификатору")
            .WithDescription("Возвращает сохранённые данные опроса в любом статусе, включая Failed и Deleted.")
            .Produces<PollResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/close", CloseAsync)
            .WithName("ClosePoll")
            .WithSummary("Закрыть опрос")
            .WithDescription("Останавливает опрос в Telegram. Повторное закрытие безопасно. Закрыть можно только опрос со статусом Sent.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeletePoll")
            .WithSummary("Удалить опрос")
            .WithDescription("Удаляет сообщение с опросом в Telegram и ставит статус Deleted. Повторное удаление безопасно.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesTelegramProblems();

        group.MapPinEndpoints(PublicationKind.Poll, "PollPin");

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreatePollRequest request,
        [FromHeader(Name = Idempotency.HeaderName), Description("Необязательный ключ идемпотентности, до 255 видимых ASCII-символов.")] string? idempotencyKey,
        IMessageBus bus,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<CreateResult<PollDetails>>(request.ToCommand(idempotencyKey), ct);
        return PublicationEndpoints.CreatedOrReplayed($"/api/polls/{result.Value.Id}", result, result.Value.ToResponse());
    }

    private static async Task<IResult> GetAsync([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<PollDetails>(new GetPollQuery(id), ct);
        return TypedResults.Ok(result.ToResponse());
    }

    private static async Task<IResult> CloseAsync([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new ClosePollCommand(id), ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteAsync([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeletePollCommand(id), ct);
        return TypedResults.NoContent();
    }
}

internal static class PinEndpoints
{
    public static void MapPinEndpoints(this RouteGroupBuilder group, PublicationKind kind, string namePrefix)
    {
        var what = kind == PublicationKind.Poll ? "опрос" : "публикацию";

        group.MapPut("/{id:guid}/pin", async (
                [Description("Внутренний идентификатор записи (Guid).")] Guid id,
                IMessageBus bus,
                CancellationToken ct,
                [Description("Закрепить без уведомления подписчиков.")] bool disableNotification = false) =>
            {
                await bus.InvokeAsync(new PinMessageCommand(kind, id, disableNotification), ct);
                return TypedResults.NoContent();
            })
            .WithName(namePrefix + "Put")
            .WithSummary($"Закрепить {what} в канале")
            .WithDescription("Закрепляет сообщение (у альбома первое). Только для статуса Sent. Повторный вызов безопасен. "
                + "Боту нужно право администратора на закрепление или редактирование сообщений.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();

        group.MapDelete("/{id:guid}/pin", async ([Description("Внутренний идентификатор записи (Guid).")] Guid id, IMessageBus bus, CancellationToken ct) =>
            {
                await bus.InvokeAsync(new UnpinMessageCommand(kind, id), ct);
                return TypedResults.NoContent();
            })
            .WithName(namePrefix + "Delete")
            .WithSummary($"Открепить {what}")
            .WithDescription("Открепляет сообщение. Только для статуса Sent. Повторный вызов безопасен.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesTelegramProblems();
    }
}

internal static class IdempotencyDocs
{
    public const string Description =
        "Необязательный заголовок Idempotency-Key (до 255 видимых ASCII-символов): повтор того же запроса с тем же ключом "
        + "не публикует сообщение заново и возвращает уже созданную запись с кодом 200; тот же ключ с другим содержимым — 409.";
}

public static class HistoryEndpoints
{
    public static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/publications-history", GetAsync)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
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
        [Description("Номер страницы, с 1.")] int page = 1,
        [Description("Размер страницы, от 1 до 100. По умолчанию 20.")] int pageSize = HistoryLimits.DefaultPageSize,
        [Description("Фильтр по статусу: Sent, Failed или Deleted.")] PublicationStatus? status = null,
        [Description("Фильтр по каналу, например @my_channel.")] string? channelAlias = null,
        [FromQuery(Name = "from"), Description("Начало периода по дате создания, включительно (ISO 8601).")] DateTimeOffset? createdFrom = null,
        [FromQuery(Name = "to"), Description("Конец периода по дате создания, включительно (ISO 8601).")] DateTimeOffset? createdTo = null)
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
        var group = app.MapGroup("/api").WithTags("Telegram")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("/bot/check", CheckBotAsync)
            .WithName("CheckBot")
            .WithSummary("Проверить токен и доступность бота")
            .WithDescription("Обращается к Telegram от имени настроенного бота. 200 — Telegram доступен и токен действителен; 502 — Telegram отклонил запрос или недоступен; 504 — Telegram не ответил вовремя.")
            .Produces<BotCheckResponse>()
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        group.MapGet("/channels/{alias}", GetChannelAsync)
            .WithName("GetChannelInfo")
            .WithSummary("Информация о канале")
            .WithDescription("Alias канала вместе с @, например @my_channel.")
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

    private static async Task<IResult> GetChannelAsync([Description("Alias канала вместе с @, например @my_channel.")] string alias, IMessageBus bus, CancellationToken ct)
    {
        var info = await bus.InvokeAsync<ChannelInfo>(new GetChannelInfoQuery(alias), ct);
        return TypedResults.Ok(info.ToResponse());
    }
}

internal static class EndpointMetadataExtensions
{
    public static RouteHandlerBuilder ProducesTelegramProblems(this RouteHandlerBuilder builder) =>
        builder
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
}
