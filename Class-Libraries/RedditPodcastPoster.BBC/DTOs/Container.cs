using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Container
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
