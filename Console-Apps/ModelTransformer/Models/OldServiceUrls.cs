using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ModelTransformer.Models;

public class OldServiceUrls
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