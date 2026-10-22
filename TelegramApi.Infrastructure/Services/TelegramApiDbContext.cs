using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using TelegramApi.Application.Common;

namespace TelegramApi.Infrastructure;
public class TelegramApiDbContext : DbContext, IApplicationDbContext
{
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<Poll> Polls => Set<Poll>();
    public TelegramApiDbContext(DbContextOptions<TelegramApiDbContext> options) : base(options) { }
}