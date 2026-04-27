using FluentAssertions;
using HackerNews.Api.Controllers;
using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace HackerNews.Api.Tests.Controllers;

public class StoriesControllerTests
{
    private static (StoriesController controller, Mock<IHackerNewsService> service) CreateSut()
    {
        var service = new Mock<IHackerNewsService>();
        var controller = new StoriesController(service.Object);
        return (controller, service);
    }

    [Fact]
    public async Task GetNewest_returns_OK_with_paged_result()
    {
        var (controller, service) = CreateSut();
        var expected = new PagedResult<Story>
        {
            Items = new[] { new Story { Id = 1, Title = "Hello" } },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };
        service.Setup(s => s.GetNewestStoriesAsync(1, 20, null, It.IsAny<CancellationToken>()))
               .ReturnsAsync(expected);

        var actionResult = await controller.GetNewest();

        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetNewest_passes_search_term_to_service()
    {
        var (controller, service) = CreateSut();
        service.Setup(s => s.GetNewestStoriesAsync(1, 20, "angular", It.IsAny<CancellationToken>()))
               .ReturnsAsync(new PagedResult<Story>())
               .Verifiable();

        await controller.GetNewest(search: "angular");

        service.Verify();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    public async Task GetNewest_returns_400_for_invalid_page(int page, int pageSize)
    {
        var (controller, _) = CreateSut();

        var actionResult = await controller.GetNewest(page: page, pageSize: pageSize);

        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetNewest_returns_400_for_invalid_pageSize(int page, int pageSize)
    {
        var (controller, _) = CreateSut();

        var actionResult = await controller.GetNewest(page: page, pageSize: pageSize);

        actionResult.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
