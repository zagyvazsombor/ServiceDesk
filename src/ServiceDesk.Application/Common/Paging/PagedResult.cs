namespace ServiceDesk.Application.Common.Paging;

// PageNumber is 1-based; TotalCount counts the items of every page. A page past the last one has no items.
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
