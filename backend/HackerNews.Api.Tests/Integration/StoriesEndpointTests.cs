using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace HackerNews.Api.Tests.Integration;

/// <summary>
/// Integration tests that boot the full pipeline via WebApplicationFactory
/// and only swap out the upstream IHackerNewsService. This validates DI wiring,
/// routing, model binding, and CORS without depending on the live Hacker News API.
/// </summary>
public class StoriesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public StoriesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(Mock<IHackerNewsService> mockService)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real service with our mock so the integration test is hermetic.
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IHackerNewsService));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton(mockService.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GET_newest_returns_200_and_paged_payload()
    {
        var mock = new Mock<IHackerNewsService>();
        mock.Setup(s => s.GetNewestStoriesAsync(1, 20, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Story>
            {
                Items = new[] { new Story { Id = 42, Title = "Hello world", Url = "https://x.com" } },
                TotalCount = 1,
                Page = 1,
                PageSize = 20
            });

        var client = CreateClient(mock);
        var response = await client.GetAsync("/api/stories/newest");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<PagedResult<Story>>();
        payload!.Items.Should().HaveCount(1);
        payload.Items[0].Title.Should().Be("Hello world");
        payload.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GET_newest_with_invalid_page_returns_400()
    {
        var client = CreateClient(new Mock<IHackerNewsService>());

        var response = await client.GetAsync("/api/stories/newest?page=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_health_returns_healthy()
    {
        var client = CreateClient(new Mock<IHackerNewsService>());

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
