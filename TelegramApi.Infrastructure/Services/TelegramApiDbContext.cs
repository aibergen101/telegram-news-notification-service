using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Infrastructure;

public class TelegramApiDbContext(DbContextOptions<TelegramApiDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public const int IdempotencyHashLength = 64;

    public DbSet<Publication> Publications => Set<Publication>();

    public DbSet<Poll> Polls => Set<Poll>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureIdempotency(modelBuilder.Entity<Publication>());
        ConfigureIdempotency(modelBuilder.Entity<Poll>());
    }

    /// <summary>ТЗ 15.3: ключ хранится вместе с записью и уникален на уровне БД.</summary>
    private static void ConfigureIdempotency<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : PublishableEntity
    {
        entity.Property(e => e.IdempotencyKey).HasMaxLength(PublishableEntity.MaxIdempotencyKeyLength);
        entity.Property(e => e.IdempotencyHash).HasMaxLength(IdempotencyHashLength);
        entity.HasIndex(e => e.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
    }
}
