using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Cloudflare;

public class MetaData
{
    [JsonPropertyName("episodeTitle")]
    public required string EpisodeTitle { get; set; }

    [JsonPropertyName("releaseDate")]
    public required DateOnly ReleaseDate { get; set; }

    [JsonPropertyName("duration")]
    public required TimeSpan Duration { get; set; }
}