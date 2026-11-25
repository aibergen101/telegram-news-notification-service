using System.Text.Json.Serialization;
using FluentValidation;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Scalar.AspNetCore;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Endpoints;
using TelegramApi.ErrorHandling;
using TelegramApi.Infrastructure;
using TelegramApi.Infrastructure.HealthChecks;
using Wolverine;
using Wolverine.FluentValidation;


const long MaxRequestBodyBytes = 150L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxRequestBodyBytes);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<CreateTextPublicationValidator>(ServiceLifetime.Singleton);

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql")
    .AddCheck<TelegramHealthCheck>("telegram", tags: [TelegramHealthCheck.Tag]);

builder.Host.UseWolverine(options =>
{
    options.Discovery.IncludeAssembly(typeof(CreateTextPublicationCommand).Assembly);
    options.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
    options.UseFluentValidation();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapOpenApi();
app.MapScalarApiReference();

// ТЗ 11.2: доступность Telegram не влияет на основной health check.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains(TelegramHealthCheck.Tag)
});
app.MapHealthChecks("/health/telegram", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(TelegramHealthCheck.Tag)
});

app.MapPublicationEndpoints();
app.MapPollEndpoints();
app.MapHistoryEndpoints();
app.MapTelegramEndpoints();

app.Run();
