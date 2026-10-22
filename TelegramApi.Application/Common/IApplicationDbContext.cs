using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Publication> Publications { get; }
    DbSet<Poll> Polls { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}