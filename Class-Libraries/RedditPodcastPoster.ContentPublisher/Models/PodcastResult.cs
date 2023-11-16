using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class PodcastResult
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

    [JsonPropertyName("subjects")]
    public string[] Subjects { get; set; }

    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";
}