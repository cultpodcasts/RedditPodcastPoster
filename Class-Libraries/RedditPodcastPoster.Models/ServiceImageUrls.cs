using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class ServiceImageUrls
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(1)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(2)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(3)]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("other")]
    [JsonPropertyOrder(4)]
    public Uri? Other { get; set; }
}