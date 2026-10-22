using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class PodcastResult
{
    [JsonPropertyName("podcastName")]
    public required string PodcastName { get; set; }

    [JsonPropertyName("episodeId")]
    public Guid EpisodeId { get; set; }

    [JsonPropertyName("episodeTitle")]
    public required string EpisodeTitle { get; set; }

    [JsonPropertyName("episodeDescription")]
    public required string EpisodeDescription { get; set; }

    [JsonPropertyName("length")]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("release")]
    public DateTime Release { get; set; }

    [JsonPropertyName("ids")]
    public EpisodeIds? Ids { get; set; }

    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("subjects")]
    public string[]? Subjects { get; set; }

    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("images")]
    public EpisodeImages? Images { get; set; } = null;

    [JsonPropertyName("knownTerms")]
    [JsonPropertyOrder(271)]
    public string[]? KnownTerms { get; set; } = null;

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>
    /// <c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c>; null for a podcast episode.
    /// <see cref="PodcastName"/> then holds the news organisation or TV show name (a film's own name).
    /// </summary>
    [JsonPropertyName("contentKind")]
    public string? ContentKind { get; set; }
}