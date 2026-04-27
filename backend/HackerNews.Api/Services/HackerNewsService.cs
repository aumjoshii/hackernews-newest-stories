using System.Net.Http.Json;
using HackerNews.Api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.Services;

public class HackerNewsServiceOptions
{
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";
    public int NewestIdsCacheSeconds { get; set; } = 60;
    public int StoryCacheMinutes { get; set; } = 10;
    /// <summary>Limit on how many parallel HTTP calls to make when hydrating story details.</summary>
    public int MaxParallelStoryFetches { get; set; } = 20;
    /// <summary>
    /// Cap on how many of the newest IDs we ever consider. The Hacker News API returns up to 500;
    /// hydrating all of them on a cold cache is expensive and risks the upstream timeout.
    /// </summary>
    public int MaxNewestIds { get; set; } = 200;
    /// <summary>Per-story HTTP call timeout in seconds.</summary>
    public int PerStoryTimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Default <see cref="IHackerNewsService"/> implementation.
/// Two-layer cache: the list of newest IDs (refreshed often) and individual
/// story payloads (cached longer, since story content rarely changes).
/// </summary>
public class HackerNewsService : IHackerNewsService
{
    private const string NewestIdsCacheKey = "hn:newest-ids";
    private static string StoryCacheKey(int id) => $"hn:story:{id}";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HackerNewsService> _logger;
    private readonly HackerNewsServiceOptions _options;

    public HackerNewsService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<HackerNewsServiceOptions> options,
        ILogger<HackerNewsService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<PagedResult<Story>> GetNewestStoriesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var allIds = await GetNewestIdsAsync(cancellationToken);

        // When searching we have to hydrate stories before we can filter by title,
        // so we fetch them all (from cache when possible) and apply the filter.
        // Without a search we can slice the ID list first and only fetch one page
        // worth of stories — much cheaper.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var allStories = await HydrateStoriesAsync(allIds, cancellationToken);
            var filtered = allStories
                .Where(s => s.Title is not null &&
                            s.Title.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pagedItems = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Story>
            {
                Items = pagedItems,
                TotalCount = filtered.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        var pageIds = allIds
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var pageStories = await HydrateStoriesAsync(pageIds, cancellationToken);

        return new PagedResult<Story>
        {
            // Re-order to match the original ID order (parallel fetches arrive out of order).
            Items = pageIds
                .Select(id => pageStories.FirstOrDefault(s => s.Id == id))
                .Where(s => s is not null)
                .Cast<Story>()
                .ToList(),
            TotalCount = allIds.Count,
            Page = page,
            PageSize = pageSize
        };
    }

    private async Task<IReadOnlyList<int>> GetNewestIdsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(NewestIdsCacheKey, out IReadOnlyList<int>? cachedIds) && cachedIds is not null)
        {
            return cachedIds;
        }

        _logger.LogInformation("Fetching newest story IDs from Hacker News");

        var ids = await _httpClient.GetFromJsonAsync<List<int>>(
            "newstories.json", cancellationToken) ?? new List<int>();

        // Cap the working set so cold-cache search calls don't fan out to 500 upstream fetches.
        if (_options.MaxNewestIds > 0 && ids.Count > _options.MaxNewestIds)
        {
            ids = ids.Take(_options.MaxNewestIds).ToList();
        }

        _cache.Set(
            NewestIdsCacheKey,
            (IReadOnlyList<int>)ids,
            TimeSpan.FromSeconds(_options.NewestIdsCacheSeconds));

        return ids;
    }

    private async Task<List<Story>> HydrateStoriesAsync(
        IReadOnlyList<int> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0) return new List<Story>();

        // Bound the level of parallelism so we don't hammer the upstream API.
        using var throttle = new SemaphoreSlim(_options.MaxParallelStoryFetches);

        var tasks = ids.Select(async id =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                return await GetStoryAsync(id, cancellationToken);
            }
            finally
            {
                throttle.Release();
            }
        });

        var stories = await Task.WhenAll(tasks);
        return stories.Where(s => s is not null).Cast<Story>().ToList();
    }

    private async Task<Story?> GetStoryAsync(int id, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(StoryCacheKey(id), out Story? cached))
        {
            return cached;
        }

        // Apply a per-call timeout so a single hung upstream request can't dominate
        // the 30-second client-wide timeout for an entire page of fetches.
        using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.PerStoryTimeoutSeconds > 0)
        {
            perCallCts.CancelAfter(TimeSpan.FromSeconds(_options.PerStoryTimeoutSeconds));
        }

        try
        {
            var story = await _httpClient.GetFromJsonAsync<Story>(
                $"item/{id}.json", perCallCts.Token);

            if (story is not null)
            {
                _cache.Set(
                    StoryCacheKey(id),
                    story,
                    TimeSpan.FromMinutes(_options.StoryCacheMinutes));
            }

            return story;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancelled — propagate so the whole request unwinds.
            throw;
        }
        catch (Exception ex)
        {
            // A single bad story (404, timeout, transient error) shouldn't kill the whole page request.
            _logger.LogWarning(ex, "Failed to fetch story {StoryId}", id);
            return null;
        }
    }
}
