using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class RecentEpisode
{
    [JsonPropertyName("podcastName")]
    public required string PodcastName { get; set; }

    [JsonPropertyName("episodeTitle")]
    public required string EpisodeTitle { get; set; }

    [JsonPropertyName("episodeDescription")]
    public required string EpisodeDescription { get; set; }

    [JsonPropertyName("length")]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("release")]
    public DateTime Release { get; set; }

    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("subject")]
    public string? Subject { get; set; }
}