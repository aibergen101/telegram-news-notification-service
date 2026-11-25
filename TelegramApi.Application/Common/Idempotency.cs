using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

/// <summary>Результат команды создания: новая запись или повтор уже выполненного запроса с тем же ключом.</summary>
public record CreateResult<T>(T Value, bool Replayed);

/// <summary>
/// Идемпотентность создания (ТЗ 15.3). Ключ приходит в заголовке Idempotency-Key и хранится в записи.
/// Эквивалентность запросов определяется по SHA-256 нормализованного содержимого команды.
/// </summary>
public static class Idempotency
{
    public const string HeaderName = "Idempotency-Key";

    /// <summary>Хэш содержимого: тот же ключ с другим хэшем означает другой запрос (409).</summary>
    public static string ComputeHash(object normalizedRequest)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalizedRequest);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>Хэш содержимого файла. Поток возвращается в начало, чтобы потом отправить его в Telegram.</summary>
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

    /// <summary>
    /// Ищет запись с этим ключом в публикациях и опросах.
    /// Тот же запрос возвращает найденную запись, другой запрос с тем же ключом — 409.
    /// </summary>
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

    /// <summary>
    /// Сохраняет новую запись. Если параллельный запрос с тем же ключом успел раньше,
    /// уникальный индекс не даст создать дубль, и возвращается его запись (или 409).
    /// </summary>
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
