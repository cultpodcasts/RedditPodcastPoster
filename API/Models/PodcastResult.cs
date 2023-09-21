using System.Text.Json.Serialization;

namespace API.Models;

public class PodcastResult
{
    [JsonPropertyName("podcastName")]
    public string PodcastName {get; set; }
    [JsonPropertyName("episodeTitle")]
    public string EpisodeTitle { get; set; }
    [JsonPropertyName("episodeDescription")]
    public string EpisodeDescription { get; set; }
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
    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";
    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";
}