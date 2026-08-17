namespace ERP.Application.Common.Models;

/// <summary>
/// Standard server-side paging envelope returned by every "list/search" use
/// case. Backs the Grid Design Standards approved in Prompt 8 (Paging,
/// Virtual Scrolling) and the Performance Principles in Prompt 0 ("always
/// minimize database round trips" / "always support multiple concurrent
/// users") - list endpoints must never return an entire unbounded table to
/// the Blazor Server UI, given the approved requirement to support
/// "unlimited transactions" (Prompt 4) over a 10+ year system lifetime.
/// </summary>
public class PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; }

    public int PageNumber { get; }

    public int TotalPages { get; }

    public int TotalCount { get; }

    public PaginatedList(IReadOnlyCollection<T> items, int totalCount, int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        TotalCount = totalCount;
        Items = items;
    }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public static PaginatedList<T> Empty(int pageNumber, int pageSize) => new(Array.Empty<T>(), 0, pageNumber, pageSize);
}
