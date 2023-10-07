using System.Text.Json.Serialization;

namespace Indexer.Dtos;

public class HomePageModel
{
    [JsonPropertyName("recentEpisodes")]
    public IEnumerable<RecentEpisode> RecentEpisodes { get; set; }

    [JsonPropertyName("episodeCount")]
    public int? EpisodeCount { get; set; }

    [JsonPropertyName("totalDuration")]
    public TimeSpan TotalDuration { get; set; }
}