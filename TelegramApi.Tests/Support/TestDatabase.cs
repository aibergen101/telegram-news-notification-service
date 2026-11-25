using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Infrastructure;

namespace TelegramApi.Tests.Support;

/// <summary>
/// SQLite в памяти: в отличие от InMemory-провайдера EF Core соблюдает уникальные индексы.
/// Соединение живёт столько же, сколько объект, поэтому несколько контекстов видят одни и те же данные.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");

    public TestDatabase()
    {
        connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TelegramApiDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TelegramApiDbContext>().UseSqlite(connection).Options);

    public void Dispose() => connection.Dispose();
}
