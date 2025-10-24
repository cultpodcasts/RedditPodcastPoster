using System.Text.Json.Serialization;

namespace RedditPodcastPoster.BBC.DTOs;

public class Properties
{
    [JsonPropertyName("isInUK")]
    public required bool IsInUK { get; set; }

    [JsonPropertyName("pageProps")]
    public required PageProperties PageProperties { get; set; }
}