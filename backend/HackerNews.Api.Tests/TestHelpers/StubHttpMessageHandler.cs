using System.Net;
using System.Text;

namespace HackerNews.Api.Tests.TestHelpers;

/// <summary>
/// HttpMessageHandler stub that returns canned JSON responses based on URL substrings.
/// Lets us unit-test <see cref="Services.HackerNewsService"/> without hitting the real API.
/// </summary>
public class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _responses;
    public int CallCount { get; private set; }
    public List<string> RequestedUrls { get; } = new();

    public StubHttpMessageHandler(Dictionary<string, string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        var url = request.RequestUri!.ToString();
        RequestedUrls.Add(url);

        var match = _responses.FirstOrDefault(kvp => url.Contains(kvp.Key));
        if (match.Key is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(match.Value, Encoding.UTF8, "application/json")
        });
    }
}
