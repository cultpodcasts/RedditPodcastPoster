using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class HomePageModel
{
    [JsonPropertyName("recentEpisodes")]
    public required IEnumerable<RecentEpisode> RecentEpisodes { get; set; }

    [JsonPropertyName("episodeCount")]
    public int EpisodeCount { get; set; }

    [JsonPropertyName("totalDuration")]
    public TimeSpan TotalDuration { get; set; }
}