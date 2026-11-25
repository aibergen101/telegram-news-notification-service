using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

/// <summary>
/// Идемпотентность создания публикаций и опросов (ТЗ 15.3).
/// Ключ приходит в заголовке Idempotency-Key и хранится в самой записи вместе с хешем запроса.
/// Повтор с тем же ключом и тем же содержимым возвращает существующую запись без новой отправки в Telegram,
/// тот же ключ с другим содержимым даёт 409.
/// </summary>
public static class Idempotency
{
    public const string HeaderName = "Idempotency-Key";

    public const int MaxKeyLength = 255;

    /// <summary>SHA-256 канонического JSON-представления запроса в виде hex-строки.</summary>
    public static string ComputeHash(object canonicalRequest) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(canonicalRequest)));

    /// <summary>
    /// Ищет запись с этим ключом. Возвращает null, если ключа нет или запись не найдена.
    /// Если ключ уже использован с другим содержимым, бросает ConflictException.
    /// </summary>
    public static async Task<T?> FindExistingAsync<T>(
        IQueryable<T> query,
        string? key,
        string? requestHash,
        CancellationToken ct)
        where T : PublishableEntity
    {
        if (key is null)
        {
            return null;
        }

        var existing = await query.FirstOrDefaultAsync(e => e.IdempotencyKey == key, ct);

        if (existing is not null && existing.RequestHash != requestHash)
        {
            throw new ConflictException(
                $"Idempotency-Key '{key}' was already used with a different request.");
        }

        return existing;
    }

    /// <summary>
    /// Сохраняет новую запись. Если параллельный запрос с тем же ключом успел сохранить свою,
    /// уникальный индекс отклонит вставку: тогда новая запись отбрасывается и возвращается существующая.
    /// Возвращает null, если сохранена новая запись.
    /// </summary>
    public static async Task<T?> SaveNewAsync<T>(
        IApplicationDbContext db,
        DbSet<T> set,
        IQueryable<T> lookup,
        T entity,
        CancellationToken ct)
        where T : PublishableEntity
    {
        set.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException) when (entity.IdempotencyKey is not null)
        {
            // Удаление ещё не сохранённой записи просто отсоединяет её вместе со вложенными сущностями.
            set.Remove(entity);

            var existing = await FindExistingAsync(lookup, entity.IdempotencyKey, entity.RequestHash, ct);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }
}
