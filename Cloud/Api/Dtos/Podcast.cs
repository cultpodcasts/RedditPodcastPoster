using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace Api.Dtos;

public class Podcast
{
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }

    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool? IndexAllEpisodes { get; set; }

    [JsonPropertyName("bypassShortEpisodeChecking")]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubePublicationDelay")]
    public long? YouTubePublishingDelayTimeSpan { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string? TwitterHandle { get; set; }

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