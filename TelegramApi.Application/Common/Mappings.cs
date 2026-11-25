using TelegramApi.Domain;

namespace TelegramApi.Application.Common;

public static class Mappings
{
    /// <summary>
    /// Id не задаётся: EF Core сгенерирует его при вставке. Иначе новые кнопки, добавленные к уже отслеживаемой
    /// публикации при редактировании, считались бы существующими строками.
    /// </summary>
    public static List<Button> ToEntities(this IReadOnlyList<ButtonDto> buttons) =>
        buttons
            .Select((b, index) => new Button { Text = b.Text, Url = b.Url, Order = index })
            .ToList();

    public static List<PollOption> ToEntities(this IReadOnlyList<PollOptionDto> options) =>
        options
            .Select((o, index) => new PollOption { Id = Guid.NewGuid(), Text = o.Text, Order = index })
            .ToList();

    public static PublicationDetails ToDetails(this Publication p) => new(
        p.Id,
        p.TgAlias,
        p.TgChatId,
        p.TgMsgId,
        p.Status,
        p.Text,
        p.Format,
        p.Buttons.OrderBy(b => b.Order).Select(b => new ButtonResult(b.Text, b.Url)).ToList(),
        p.MediaFiles.OrderBy(m => m.Order)
            .Select(m => new MediaFileResult(m.FileName, m.MediaType, m.TgFileId, m.TgMessageId, m.Order))
            .ToList(),
        p.IsPinned,
        p.CreatedAt,
        p.SentAt,
        p.EditedAt,
        p.DeletedAt,
        p.UpdatedAt,
        p.ErrorCode,
        p.ErrorText,
        p.ErrorDate);

    public static PollDetails ToDetails(this Poll p) => new(
        p.Id,
        p.TgAlias,
        p.TgChatId,
        p.TgMsgId,
        p.Status,
        p.Question,
        p.Options.OrderBy(o => o.Order).Select(o => new PollOptionResult(o.Text, o.Order)).ToList(),
        p.AllowMultipleAnswers,
        p.IsAnonymous,
        p.Duration,
        p.IsClosed,
        p.IsPinned,
        p.CreatedAt,
        p.SentAt,
        p.ClosedAt,
        p.DeletedAt,
        p.UpdatedAt,
        p.ErrorCode,
        p.ErrorText,
        p.ErrorDate);
}
