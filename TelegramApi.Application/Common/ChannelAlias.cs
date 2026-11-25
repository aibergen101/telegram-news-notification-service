namespace TelegramApi.Application.Common;

/// <summary>Правила записи публичного имени канала (@username).</summary>
public static class ChannelAlias
{
    /// <summary>Имя канала с @ или без: 5–32 символа, начинается с буквы, далее буквы, цифры и подчёркивание.</summary>
    public const string Pattern = @"^@?[A-Za-z][A-Za-z0-9_]{4,31}$";

    public static string Normalize(string alias)
    {
        var trimmed = alias.Trim();
        return trimmed.StartsWith('@') ? trimmed : "@" + trimmed;
    }
}
