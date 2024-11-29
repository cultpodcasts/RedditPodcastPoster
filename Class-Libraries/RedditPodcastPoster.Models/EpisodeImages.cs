using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class EpisodeImages
{
    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(1)]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(3)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(3)]
    public Uri? Apple { get; set; }
}