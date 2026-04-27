using FluentAssertions;
using HackerNews.Api.Services;
using HackerNews.Api.Tests.TestHelpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HackerNews.Api.Tests.Services;

public class HackerNewsServiceTests
{
    private static HackerNewsService CreateService(
        StubHttpMessageHandler handler,
        IMemoryCache? cache = null,
        HackerNewsServiceOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://hacker-news.firebaseio.com/v0/")
        };

        var memoryCache = cache ?? new MemoryCache(new MemoryCacheOptions());
        var opts = Options.Create(options ?? new HackerNewsServiceOptions
        {
            NewestIdsCacheSeconds = 60,
            StoryCacheMinutes = 10,
            MaxParallelStoryFetches = 5
        });

        return new HackerNewsService(httpClient, memoryCache, opts, NullLogger<HackerNewsService>.Instance);
    }

    private static Dictionary<string, string> BuildResponses(int storyCount)
    {
        var ids = Enumerable.Range(1, storyCount).ToList();
        var responses = new Dictionary<string, string>
        {
            ["newstories.json"] = "[" + string.Join(",", ids) + "]"
        };

        foreach (var id in ids)
        {
            // Mark every 3rd story as having no URL — exercises the "missing url" case from the requirements.
            var url = id % 3 == 0 ? "null" : $"\"https://example.com/{id}\"";
            responses[$"item/{id}.json"] =
                $@"{{
                    ""id"": {id},
                    ""title"": ""Story {id}"",
                    ""url"": {url},
                    ""by"": ""user{id}"",
                    ""score"": {id * 10},
                    ""time"": 1700000000,
                    ""type"": ""story""
                }}";
        }

        return responses;
    }

    [Fact]
    public async Task GetNewestStoriesAsync_returns_first_page_in_id_order()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(10));
        var service = CreateService(handler);

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 5, search: null);

        result.Items.Should().HaveCount(5);
        result.TotalCount.Should().Be(10);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(5);
        result.TotalPages.Should().Be(2);
        result.Items.Select(s => s.Id).Should().ContainInOrder(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task GetNewestStoriesAsync_returns_correct_second_page()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(10));
        var service = CreateService(handler);

        var result = await service.GetNewestStoriesAsync(page: 2, pageSize: 5, search: null);

        result.Items.Select(s => s.Id).Should().ContainInOrder(6, 7, 8, 9, 10);
    }

    [Fact]
    public async Task GetNewestStoriesAsync_handles_stories_without_url()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(6));
        var service = CreateService(handler);

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 6, search: null);

        // Stories 3 and 6 have null url per the test data setup.
        result.Items.Single(s => s.Id == 3).Url.Should().BeNull();
        result.Items.Single(s => s.Id == 6).Url.Should().BeNull();
        result.Items.Single(s => s.Id == 1).Url.Should().Be("https://example.com/1");
    }

    [Fact]
    public async Task GetNewestStoriesAsync_filters_by_search_term_case_insensitively()
    {
        var responses = new Dictionary<string, string>
        {
            ["newstories.json"] = "[1,2,3,4]",
            ["item/1.json"] = @"{ ""id"": 1, ""title"": ""Angular tips"", ""url"": ""https://a.com"", ""type"": ""story"" }",
            ["item/2.json"] = @"{ ""id"": 2, ""title"": ""React patterns"", ""url"": ""https://b.com"", ""type"": ""story"" }",
            ["item/3.json"] = @"{ ""id"": 3, ""title"": ""ANGULAR forms"", ""url"": ""https://c.com"", ""type"": ""story"" }",
            ["item/4.json"] = @"{ ""id"": 4, ""title"": ""Vue intro"", ""url"": ""https://d.com"", ""type"": ""story"" }"
        };
        var service = CreateService(new StubHttpMessageHandler(responses));

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 10, search: "angular");

        result.TotalCount.Should().Be(2);
        result.Items.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public async Task GetNewestStoriesAsync_caches_newest_ids_between_calls()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(5));
        var service = CreateService(handler);

        await service.GetNewestStoriesAsync(page: 1, pageSize: 5, search: null);
        var idsCallsAfterFirst = handler.RequestedUrls.Count(u => u.Contains("newstories.json"));

        await service.GetNewestStoriesAsync(page: 1, pageSize: 5, search: null);
        var idsCallsAfterSecond = handler.RequestedUrls.Count(u => u.Contains("newstories.json"));

        idsCallsAfterFirst.Should().Be(1);
        idsCallsAfterSecond.Should().Be(1, "newstories.json should be served from cache on the second call");
    }

    [Fact]
    public async Task GetNewestStoriesAsync_caches_individual_stories()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(5));
        var service = CreateService(handler);

        await service.GetNewestStoriesAsync(page: 1, pageSize: 5, search: null);
        var firstStoryCalls = handler.RequestedUrls.Count(u => u.Contains("item/1.json"));

        await service.GetNewestStoriesAsync(page: 1, pageSize: 5, search: null);
        var secondStoryCalls = handler.RequestedUrls.Count(u => u.Contains("item/1.json"));

        firstStoryCalls.Should().Be(1);
        secondStoryCalls.Should().Be(1, "story details should be cached");
    }

    [Fact]
    public async Task GetNewestStoriesAsync_clamps_invalid_paging_input()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(5));
        var service = CreateService(handler);

        var result = await service.GetNewestStoriesAsync(page: -1, pageSize: 0, search: null);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetNewestStoriesAsync_returns_empty_when_no_stories_match_search()
    {
        var handler = new StubHttpMessageHandler(BuildResponses(5));
        var service = CreateService(handler);

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 10, search: "no-such-string-zzz");

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetNewestStoriesAsync_caps_working_set_to_MaxNewestIds()
    {
        // Stub returns 50 IDs but we cap to 10 — only the first 10 should ever be considered.
        var handler = new StubHttpMessageHandler(BuildResponses(50));
        var service = CreateService(handler, options: new HackerNewsServiceOptions
        {
            NewestIdsCacheSeconds = 60,
            StoryCacheMinutes = 10,
            MaxParallelStoryFetches = 5,
            MaxNewestIds = 10,
            PerStoryTimeoutSeconds = 10
        });

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 100, search: null);

        result.TotalCount.Should().Be(10, "the cap should bound the set of considered IDs");
        result.Items.Select(s => s.Id).Should().ContainInOrder(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        // Make sure we never fetched item/11 onwards.
        handler.RequestedUrls.Should().NotContain(u => u.Contains("item/11.json"));
    }

    [Fact]
    public async Task GetNewestStoriesAsync_skips_stories_that_fail_to_fetch()
    {
        // Story 2 is intentionally missing from the stub — the handler returns 404 for it.
        var responses = new Dictionary<string, string>
        {
            ["newstories.json"] = "[1,2,3]",
            ["item/1.json"] = @"{ ""id"": 1, ""title"": ""One"", ""url"": ""https://a.com"", ""type"": ""story"" }",
            ["item/3.json"] = @"{ ""id"": 3, ""title"": ""Three"", ""url"": ""https://c.com"", ""type"": ""story"" }"
        };
        var service = CreateService(new StubHttpMessageHandler(responses));

        var result = await service.GetNewestStoriesAsync(page: 1, pageSize: 10, search: null);

        result.Items.Select(s => s.Id).Should().BeEquivalentTo(new[] { 1, 3 });
        result.TotalCount.Should().Be(3, "TotalCount reflects the ID list, not how many we successfully fetched");
    }
}
