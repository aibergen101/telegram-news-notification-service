using TelegramApi.Application.Common;
using TelegramApi.Application.Polls.CreatePoll;
using TelegramApi.Application.Publications.CreateMedia;
using TelegramApi.Application.Publications.CreateText;
using TelegramApi.Application.Publications.EditPublication;
using TelegramApi.Contracts;

namespace TelegramApi.Mapping;

public static class ApiMappings
{
    public static CreateTextPublicationCommand ToCommand(this CreateTextPublicationRequest request, string? idempotencyKey) => new(
        request.ChannelAlias,
        request.Text,
        request.Format,
        request.Buttons.ToDtos(),
        idempotencyKey);

    public static EditPublicationCommand ToCommand(this EditPublicationRequest request, Guid id) => new(
        id,
        string.IsNullOrWhiteSpace(request.Text) ? null : request.Text,
        request.Format,
        request.Buttons.ToDtos());

    public static CreateQuestionPollCommand ToCommand(this CreatePollRequest request, string? idempotencyKey) => new(
        request.ChannelAlias,
        request.Question,
        (request.Options ?? []).Select(o => new PollOptionDto(o.Text)).ToList(),
        request.AllowMultipleAnswers,
        request.IsAnonymous,
        request.Duration,
        idempotencyKey);

    public static CreateMediaPublicationCommand ToCommand(this CreateMediaPublicationRequest request, string? idempotencyKey) => new(
        request.ChannelAlias,
        string.IsNullOrWhiteSpace(request.Text) ? null : request.Text,
        request.Format,
        (request.Files ?? new FormFileCollection())
            .Select(f => new MediaFileDto(f.FileName, f.OpenReadStream(), f.ContentType, f.Length))
            .ToList(),
        idempotencyKey);

    private static List<ButtonDto> ToDtos(this List<ButtonRequest>? buttons) =>
        (buttons ?? []).Select(b => new ButtonDto(b.Text, b.Url)).ToList();

    public static PublicationResponse ToResponse(this PublicationDetails p) => new(
        p.Id,
        p.ChannelAlias,
        p.TgChatId,
        p.TgMessageId,
        p.Status,
        p.Text,
        p.Format,
        p.Buttons.Select(b => new ButtonResponse(b.Text, b.Url)).ToList(),
        p.MediaFiles.Select(m => new MediaFileResponse(m.FileName, m.MediaType, m.TgFileId, m.TgMessageId, m.Order)).ToList(),
        p.IsPinned,
        p.CreatedAt,
        p.SentAt,
        p.EditedAt,
        p.DeletedAt,
        p.UpdatedAt,
        p.ErrorCode,
        p.ErrorText,
        p.ErrorDate);

    public static PollResponse ToResponse(this PollDetails p) => new(
        p.Id,
        p.ChannelAlias,
        p.TgChatId,
        p.TgMessageId,
        p.Status,
        p.Question,
        p.Options.Select(o => new PollOptionResponse(o.Text, o.Order)).ToList(),
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

    public static HistoryItemResponse ToResponse(this HistoryItemResult item) => new(
        item.Id, item.Kind, item.ChannelAlias, item.Text, item.Status, item.CreatedAt, item.SentAt, item.DeletedAt);

    public static PagedResponse<HistoryItemResponse> ToResponse(this PagedResult<HistoryItemResult> page) => new(
        page.Items.Select(i => i.ToResponse()).ToList(), page.Page, page.PageSize, page.TotalCount);

    public static ChannelInfoResponse ToResponse(this ChannelInfo info) => new(
        info.ChatId, info.Title, info.Username, info.Type);
}
