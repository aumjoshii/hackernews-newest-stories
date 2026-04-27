using HackerNews.Api.Models;

namespace HackerNews.Api.Services;

/// <summary>
/// Abstraction over the Hacker News API. Defined as an interface so
/// controllers depend only on the contract — useful for DI and testing.
/// </summary>
public interface IHackerNewsService
{
    /// <summary>
    /// Returns a page of the newest stories, optionally filtered by a search term
    /// matched against story titles (case-insensitive).
    /// </summary>
    Task<PagedResult<Story>> GetNewestStoriesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default);
}
