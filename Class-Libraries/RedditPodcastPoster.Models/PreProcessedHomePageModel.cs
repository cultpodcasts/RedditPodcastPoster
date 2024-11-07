using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class PreProcessedHomePageModel
{
    [JsonPropertyName("totalDurationDays")]
    public required int TotalDurationDays { get; set; }

    [JsonPropertyName("episodesByDay")]
    public required Dictionary<string, RecentEpisode[]> EpisodesByDay { get; set; }

    [JsonPropertyName("hasNext")]
    public required bool HasNext { get; set; }

    [JsonPropertyName("episodesThisWeek")]
    public required int EpisodesThisWeek { get; set; }

    [JsonPropertyName("episodeCount")]
    public required int EpisodeCount { get; set; }
}