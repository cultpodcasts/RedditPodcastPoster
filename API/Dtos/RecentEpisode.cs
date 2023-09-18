using System.Text.Json.Serialization;

namespace API.Dtos;

public class RecentEpisode
{
    [JsonPropertyName("podcastName")]
    public string PodcastName { get; set; }
    [JsonPropertyName("episodeTitle")]
    public string EpisodeTitle { get; set; }
    [JsonPropertyName("episodeDescription")]
    public string EpisodeDescription { get; set; }
    [JsonPropertyName("release")]
    public DateTime Release { get; set; }
    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }
    [JsonPropertyName("apple")]
    public Uri? Apple { get; set; }
    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }
}