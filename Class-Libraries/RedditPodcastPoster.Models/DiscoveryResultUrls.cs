using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class DiscoveryResultUrls
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(10)]
    public Uri? Spotify { get; set; } = null;

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(20)]
    public Uri? Apple { get; set; } = null;

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(30)]
    public Uri? YouTube { get; set; } = null;

    public bool Any()
    {
        return Spotify != null || Apple != null || YouTube != null;
    }
}