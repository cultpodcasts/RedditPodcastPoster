using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

public class RecentEpisode
{
    [JsonIgnore] private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    [JsonPropertyName("episodeId")]
    public required Guid Id { get; set; }

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

    [JsonPropertyName("releaseDayDisplay")]
    public string ReleaseDayDisplay => TimeZoneInfo
        .ConvertTime(Release, TimeZoneInfo.Utc, London)
        .ToString("dddd d MMMM");

    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("image")]
    public Uri? Image { get; set; } = null;
}