using System.Net;
using System.Text;
using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public static class MessageText
{
    private const string Separator = "\n\n";

    private const string MarkdownV2Special = "_*[]()~`>#+-=|{}.!\\";

    public static string? Compose(string? text, string? link, TextFormat format)
    {
        if (string.IsNullOrEmpty(link))
        {
            return text;
        }

        var renderedLink = format switch
        {
            TextFormat.Html => $"<a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(link)}</a>",
            TextFormat.MarkdownV2 => $"[{Escape(link, MarkdownV2Special)}]({Escape(link, ")\\")})",
            _ => link
        };

        return string.IsNullOrEmpty(text) ? renderedLink : text + Separator + renderedLink;
    }

    public static int Length(string? text, string? link)
    {
        var textLength = text?.Length ?? 0;
        if (string.IsNullOrEmpty(link))
        {
            return textLength;
        }

        return textLength == 0 ? link.Length : textLength + Separator.Length + link.Length;
    }

    private static string Escape(string value, string special)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (special.Contains(c, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
