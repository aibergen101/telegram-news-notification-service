using FluentValidation;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Telegram.Bot;
using TelegramApi.Application.Common;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreatePoll;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Application.Publications.DeletePublication;
using TelegramApi.Contracts;
using TelegramApi.Domain;
using TelegramApi.Infrastructure;
using Wolverine;
using Wolverine.FluentValidation;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString=builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TelegramApiDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    return new TelegramBotClient(options.BotToken);
});
builder.Services.Configure<TelegramBotOptions>(builder.Configuration.GetSection(TelegramBotOptions.SectionName));
builder.Services.AddValidatorsFromAssemblyContaining<CreateTextPublicationValidator>();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(CreateTextPublicationCommand).Assembly);
    opts.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    opts.UseFluentValidation();
});

builder.Services.AddScoped<IApplicationDbContext, TelegramApiDbContext>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (ex is ValidationException vex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Validation failed",
                status = 400,
                errors = vex.Errors.Select(e => e.ErrorMessage)
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "An unexpected error occurred",
            status = 500,
            detail = ex?.Message
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapPost("/api/publications", async (CreateTextPublicationRequest request, IMessageBus bus) =>
{
    var command = new CreateTextPublicationCommand(request.ChannelAlias, request.Text, request.Button?.Select(x=>new ButtonDto(x.Text,x.Url)).ToList());
    Guid id = await bus.InvokeAsync<Guid>(command);
    return Results.Created($"/api/publications/{id}", id);
}).WithSummary("Create a text publication");

app.MapPost("/api/media-publications", async (
    HttpRequest httpRequest,
    IMessageBus bus) =>
{
    var form = await httpRequest.ReadFormAsync();

    var channelAlias = form["channelAlias"].ToString();
    var text = form["text"].ToString();
    if (string.IsNullOrWhiteSpace(text)) text = null;

    List<ButtonRequest>? buttonRequests = null;
    if (form.TryGetValue("buttons", out var buttonsJson) && !string.IsNullOrWhiteSpace(buttonsJson))
    {
        buttonRequests = System.Text.Json.JsonSerializer.Deserialize<List<ButtonRequest>>(
            buttonsJson.ToString(),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    var mediaFiles = new List<MediaFileDto>();
    foreach (var file in form.Files)
    {
        mediaFiles.Add(new MediaFileDto(
            file.FileName,
            file.OpenReadStream(),
            file.ContentType));
    }

    var buttons = buttonRequests?.Select(b => new ButtonDto(b.Text, b.Url)).ToList();

    var command = new CreateMediaPublicationCommand(channelAlias, text, buttons, mediaFiles);
    var id = await bus.InvokeAsync<Guid>(command);

    return Results.Created($"/api/media-publications/{id}", id);
}).WithSummary("Create a publication with photo and/or document attachments");

app.MapPost("/api/polls", async (CreatePollRequest request, IMessageBus bus) =>
{
    var options = request.Options.Select(o => new PollOptionDto(o.Text)).ToList();
    var command = new CreateQuestionPollCommand(
        request.ChannelAlias,
        request.Question,
        options,
        request.AllowMultipleAnswers,
        request.IsAnonymous,
        request.Duration);

    var id = await bus.InvokeAsync<Guid>(command);
    return Results.Created($"/api/polls/{id}", id);
}).WithSummary("Create a poll");

app.MapPost("/api/polls/{id}/close", async (Guid id, IMessageBus bus) =>
{
    await bus.InvokeAsync(new ClosePollCommand(id));
    return Results.NoContent();
}).WithSummary("Close a poll early");

app.MapGet("/api/publications-history", async (
    HttpRequest request,
    IApplicationDbContext db) =>
{
    int page = int.TryParse(request.Query["page"], out var p) && p > 0 ? p : 1;
    int pageSize = int.TryParse(request.Query["pageSize"], out var ps) && ps > 0 ? Math.Min(ps, 100) : 20;

    string? status = request.Query["status"];
    string? channelAlias = request.Query["channelAlias"];
    DateTime? from = DateTime.TryParse(request.Query["from"], null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var f) ? f : null;
    DateTime? to = DateTime.TryParse(request.Query["to"], null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var t) ? t : null;

    var publications = db.Publications.AsQueryable();
    var polls = db.Polls.AsQueryable();

    if (!string.IsNullOrWhiteSpace(channelAlias))
    {
        publications = publications.Where(x => x.TgAlias == channelAlias);
        polls = polls.Where(x => x.TgAlias == channelAlias);
    }

    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PublicationStatus>(status, true, out var statusEnum))
    {
        publications = publications.Where(x => x.Status == statusEnum);
        polls = polls.Where(x => x.Status == statusEnum);
    }

    if (from.HasValue)
    {
        publications = publications.Where(x => x.CreatedAt >= from.Value);
        polls = polls.Where(x => x.CreatedAt >= from.Value);
    }

    if (to.HasValue)
    {
        publications = publications.Where(x => x.CreatedAt <= to.Value);
        polls = polls.Where(x => x.CreatedAt <= to.Value);
    }

    var pubItems = await publications.Select(x => new HistoryItemResponse(
        x.Id, "Publication", x.TgAlias, x.Text, x.Status.ToString(), x.CreatedAt, x.SentAt, x.DeletedAt)).ToListAsync();

    var pollItems = await polls.Select(x => new HistoryItemResponse(
        x.Id, "Poll", x.TgAlias, x.Question, x.Status.ToString(), x.CreatedAt, null, x.DeletedAt)).ToListAsync();

    var all = pubItems.Concat(pollItems)
        .OrderByDescending(x => x.CreatedAt)
        .ToList();

    var totalCount = all.Count;
    var pagedItems = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    return Results.Ok(new PagedHistoryResponse(pagedItems, page, pageSize, totalCount));
}).WithSummary("Get publication and poll history with filters and pagination");

app.MapDelete("/api/publications/{id}", async (Guid id, IMessageBus bus) =>
{
    await bus.InvokeAsync(new DeletePublicationCommand(id));
    return Results.NoContent();
}).WithSummary("Delete a publication (idempotent)");

app.MapDelete("/api/polls/{id}", async (Guid id, IMessageBus bus) =>
{
    await bus.InvokeAsync(new DeletePollCommand(id));
    return Results.NoContent();
}).WithSummary("Delete a poll (idempotent)");


app.MapGet("/api/bot/check", async (ITelegramService tg) =>
{
    var isOk = await tg.CheckBotAsync();
    return isOk ? Results.Ok(new { status = "ok" }) : Results.Problem("Telegram bot token is invalid or unreachable", statusCode: 503);
}).WithSummary("Check that the Telegram bot token is valid");

app.MapGet("/api/channels/{alias}", async (string alias, ITelegramService tg) =>
{
    var info = await tg.GetChannelInfoAsync(alias);
    return info is null
        ? Results.Problem("Channel not found or not accessible to the bot", statusCode: 404)
        : Results.Ok(info);
}).WithSummary("Get information about a Telegram channel"); ;

app.Run();
