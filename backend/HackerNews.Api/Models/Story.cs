using System.Text.Json.Serialization;

namespace HackerNews.Api.Models;

/// <summary>
/// Represents a Hacker News story as returned by the official Hacker News API.
/// </summary>
public class Story
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The URL of the story. May be null for "Ask HN" or text-only posts.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("by")]
    public string? By { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    /// <summary>
    /// Unix timestamp (seconds) of when the story was created.
    /// </summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("descendants")]
    public int? Descendants { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
