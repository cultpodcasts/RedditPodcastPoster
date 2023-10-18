using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class ServiceUrls
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(1)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(1)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(1)]
    public Uri? YouTube { get; set; }
}