using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using TelegramApi.Application.Common;
using TelegramApi.Infrastructure;

namespace TelegramApi.Tests.Integration;

/// <summary>
/// Поднимает API с настоящим PostgreSQL в Docker (Testcontainers) и поддельным Telegram.
/// Миграции применяются так же, как это делает dotnet ef database update.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeTelegramService Telegram { get; } = new();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TelegramApiDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await postgres.DisposeAsync();
        await DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", postgres.GetConnectionString());
        builder.UseSetting("TelegramBot:BotToken", "123456:test-token");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITelegramService>();
            services.AddSingleton<ITelegramService>(Telegram);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
