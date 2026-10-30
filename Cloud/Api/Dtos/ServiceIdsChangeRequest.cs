using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Dtos;

/// <summary>
/// PATCH body shared by news organisations and TV shows: the publisher fields plus the service ids, stored under
/// the parent's <c>indexing</c> block (<c>CatalogueIndexingSource</c>, #1050). The wire names and rules are the
/// podcast edit API's (<see cref="PodcastChangeRequest"/>). Null means unchanged and <c>""</c> clears. URL
/// submission uses these ids to attach a submitted episode to the outlet or show, instead of a podcast.
/// </summary>
public abstract class ServiceIdsChangeRequest : PublisherFieldsChangeRequest
{
    [JsonPropertyName("youTubeChannelId")]
    public string? YouTubeChannelId { get; set; }

    [JsonPropertyName("youTubePlaylistId")]
    public string? YouTubePlaylistId { get; set; }

    [JsonPropertyName("spotifyId")]
    public string? SpotifyId { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("nullAppleId")]
    public bool? NullAppleId { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool? IndexAllEpisodes { get; set; }

    [JsonPropertyName("titleRegex")]
    public string? TitleRegex { get; set; }

    [JsonPropertyName("descriptionRegex")]
    public string? DescriptionRegex { get; set; }

    [JsonPropertyName("episodeMatchRegex")]
    public string? EpisodeMatchRegex { get; set; }

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string? EpisodeIncludeTitleRegex { get; set; }

    /// <summary><c>true</c> sets; <c>false</c> clears (stored as unset), as the podcast edit API.</summary>
    [JsonPropertyName("ignoreAllEpisodes")]
    public bool? IgnoreAllEpisodes { get; set; }

    [JsonPropertyName("bypassShortEpisodeChecking")]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("alwaysPromoteAsHero")]
    public bool? AlwaysPromoteAsHero { get; set; }

    /// <summary>A <see cref="TimeSpan"/> string; <c>""</c> clears. An unparseable value is logged and clears.</summary>
    [JsonPropertyName("minimumDuration")]
    public string? MinimumDuration { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("unsetReleaseAuthority")]
    public bool? UnsetReleaseAuthority { get; set; }

    /// <summary>A <see cref="TimeSpan"/> string (may be negative); <c>""</c> clears. An unparseable value is logged and ignored.</summary>
    [JsonPropertyName("youTubePublicationDelay")]
    public string? YouTubePublicationDelay { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }
}
