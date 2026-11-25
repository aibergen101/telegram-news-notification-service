using Microsoft.EntityFrameworkCore;
using TelegramApi.Application.Common;
using TelegramApi.Domain;

namespace TelegramApi.Infrastructure;

public class TelegramApiDbContext(DbContextOptions<TelegramApiDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Publication> Publications => Set<Publication>();

    public DbSet<Poll> Polls => Set<Poll>();
}
