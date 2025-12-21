using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

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

    [JsonPropertyName("spotify")]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("bbc")]
    public Uri? BBC { get; set; }

    [JsonPropertyName("internetArchive")]
    public Uri? InternetArchive { get; set; }

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
}