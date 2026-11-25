using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public record CreateResult<T>(T Value, bool Replayed);

public static class Idempotency
{
    public const string HeaderName = "Idempotency-Key";

    public static string ComputeHash(object normalizedRequest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalizedRequest);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    public static async Task<string> HashStreamAsync(Stream stream, CancellationToken ct)
    {
        if (!stream.CanSeek)
        {
            return string.Empty;
        }

        var start = stream.Position;
        var hash = await SHA256.HashDataAsync(stream, ct);
        stream.Position = start;
        return Convert.ToHexStringLower(hash);
    }

    public static async Task<T?> FindReplayAsync<T>(
        IApplicationDbContext db,
        string key,
        string hash,
        CancellationToken ct)
        where T : PublishableEntity
    {
        PublishableEntity? existing = await db.Publications.AsNoTracking()
            .Include(p => p.Buttons)
            .Include(p => p.MediaFiles)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);

        existing ??= await db.Polls.AsNoTracking()
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.IdempotencyKey == key, ct);

        if (existing is null)
        {
            return null;
        }

        if (existing is not T match || existing.IdempotencyHash != hash)
        {
            throw new ConflictException(
                $"Idempotency key '{key}' was already used with a different request.");
        }

        return match;
    }

    public static async Task<T?> SaveNewAsync<T>(
        T entity,
        IApplicationDbContext db,
        CancellationToken ct)
        where T : PublishableEntity
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException) when (entity.IdempotencyKey is { } key)
        {
            var winner = await FindReplayAsync<T>(db, key, entity.IdempotencyHash ?? string.Empty, ct);
            if (winner is null)
            {
                throw;
            }

            return winner;
        }
    }
}

public static class IdempotencyValidation
{
    public static IRuleBuilderOptions<T, string?> MustBeIdempotencyKey<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty()
            .MaximumLength(PublishableEntity.MaxIdempotencyKeyLength)
            .Must(key => key!.All(c => c is >= '!' and <= '~'))
            .WithMessage($"Idempotency key must be 1–{PublishableEntity.MaxIdempotencyKeyLength} visible ASCII characters.");
}
