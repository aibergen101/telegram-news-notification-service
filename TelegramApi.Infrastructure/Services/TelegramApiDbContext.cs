using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Infrastructure;

public class TelegramApiDbContext(DbContextOptions<TelegramApiDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Publication> Publications => Set<Publication>();

    public DbSet<Poll> Polls => Set<Poll>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ТЗ 15.3: ключ идемпотентности уникален на уровне БД. NULL-значения уникальность не нарушают.
        modelBuilder.Entity<Publication>()
            .HasIndex(p => p.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<Poll>()
            .HasIndex(p => p.IdempotencyKey)
            .IsUnique();
    }
}
