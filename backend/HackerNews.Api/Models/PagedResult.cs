namespace HackerNews.Api.Models;

/// <summary>
/// Generic wrapper used by the API to return a single page of results
/// alongside metadata the client needs to render paging UI.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
