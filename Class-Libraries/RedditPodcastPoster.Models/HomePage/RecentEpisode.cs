using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Models.HomePage;

public class RecentEpisode
{
    [JsonIgnore] private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    [JsonPropertyName("episodeId")]
    public required Guid EpisodeId { get; set; }

    [JsonPropertyName("id")]
    public Guid Id => EpisodeId;

    [JsonPropertyName("podcastName")]
    public required string PodcastName { get; set; }

    [JsonPropertyName("episodeTitle")]
    public required string EpisodeTitle { get; set; }

    [JsonPropertyName("episodeDescription")]
    public required string EpisodeDescription { get; set; }

    [JsonPropertyName("length")]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Duration => Length;

    [JsonPropertyName("release")]
    public DateTime Release { get; set; }

    [JsonPropertyName("releaseDayDisplay")]
    public string ReleaseDayDisplay => TimeZoneInfo
        .ConvertTime(Release, TimeZoneInfo.Utc, London)
        .ToString("dddd d MMMM");

    [JsonPropertyName("ids")]
    public EpisodeIds? Ids { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, EpisodeServiceLink>? Services { get; set; }

    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("image")]
    public Uri? Image { get; set; } = null;

    /// <summary>
    /// IETF language tag when non-English. Null/absent means English (or unknown treated as English).
    /// </summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }
}