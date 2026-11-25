using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Infrastructure;

namespace TelegramApi.Tests.Common;

/// <summary>
/// Реляционная БД SQLite в памяти: живёт, пока открыто соединение.
/// Каждый тест получает чистую базу; новый контекст на том же соединении проверяет, что именно сохранилось.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TelegramApiDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TelegramApiDbContext>()
            .UseSqlite(_connection)
            .Options);

    public void Dispose() => _connection.Dispose();
}
