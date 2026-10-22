namespace TelegramApi.Contracts;

public record PagedHistoryResponse(List<HistoryItemResponse> Items, int Page, int PageSize, int TotalCount);