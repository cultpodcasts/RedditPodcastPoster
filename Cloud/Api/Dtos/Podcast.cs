using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class Podcast
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("podcastName")]
    public string? Name { get; set; }

    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool? IndexAllEpisodes { get; set; }

    [JsonPropertyName("bypassShortEpisodeChecking")]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("unsetReleaseAuthority")]
    public bool? UnsetReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("unsetPrimaryPostService")]
    public bool? UnsetPrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("nullAppleId")]
    public bool? NullAppleId { get; set; }

    [JsonPropertyName("youTubePublicationDelay")]
    public string? YouTubePublishingDelayTimeSpan { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

    [JsonPropertyName("blueskyHandle")]
    public string? BlueskyHandle { get; set; }

    [JsonPropertyName("titleRegex")]
    public string? TitleRegex { get; set; }

    [JsonPropertyName("descriptionRegex")]
    public string? DescriptionRegex { get; set; }

    [JsonPropertyName("episodeMatchRegex")]
    public string? EpisodeMatchRegex { get; set; }

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string? EpisodeIncludeTitleRegex { get; set; }

    [JsonPropertyName("defaultSubject")]
    public string? DefaultSubject { get; set; }
}