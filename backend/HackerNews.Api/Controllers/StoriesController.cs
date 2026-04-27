using HackerNews.Api.Models;
using HackerNews.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StoriesController : ControllerBase
{
    private readonly IHackerNewsService _hackerNewsService;

    public StoriesController(IHackerNewsService hackerNewsService)
    {
        _hackerNewsService = hackerNewsService;
    }

    /// <summary>
    /// Returns a paged list of the newest Hacker News stories,
    /// optionally filtered by a search term applied to the title.
    /// </summary>
    [HttpGet("newest")]
    [ProducesResponseType(typeof(PagedResult<Story>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<Story>>> GetNewest(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return BadRequest(new { error = "page must be >= 1" });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { error = "pageSize must be between 1 and 100" });
        }

        var result = await _hackerNewsService.GetNewestStoriesAsync(
            page, pageSize, search, cancellationToken);

        return Ok(result);
    }
}
