using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Dtos;

/// <summary>Curator GET shared by news organisations and TV shows: the publisher fields plus the service ids from <c>indexing</c>.</summary>
public abstract class ServiceIdsDto : PublisherFieldsDto
{
    [JsonPropertyName("youTubeChannelId")]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistId")]
    public string YouTubePlaylistId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistIdHistory")]
    public YouTubePlaylistIdHistoryEntry[]? YouTubePlaylistIdHistory { get; set; }

    [JsonPropertyName("spotifyId")]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    /// <summary>From <c>indexing</c>: auto-index every episode, or only titles matching the include regex.</summary>
    [JsonPropertyName("indexAllEpisodes")]
    public bool IndexAllEpisodes { get; set; }

    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

    /// <summary>From <c>indexing</c>: new playables are stored ignored (podcast <c>ignoreAllEpisodes</c>).</summary>
    [JsonPropertyName("ignoreAllEpisodes")]
    public bool? IgnoreAllEpisodes { get; set; }

    /// <summary>
    /// From <c>indexing</c>, as the indexer applies it: unset reads as on for a news organisation
    /// (<c>CatalogueIndexingSource.EffectiveBypassShortEpisodeChecking</c>) and as unset for a TV show.
    /// </summary>
    [JsonPropertyName("bypassShortEpisodeChecking")]
    public bool? BypassShortEpisodeChecking { get; set; }

    /// <summary>From <c>indexing</c>: new playables are appended to the homepage hero list.</summary>
    [JsonPropertyName("alwaysPromoteAsHero")]
    public bool? AlwaysPromoteAsHero { get; set; }

    /// <summary>From <c>indexing</c>, a <see cref="TimeSpan"/> string (e.g. <c>00:01:30</c>); null when unset.</summary>
    [JsonPropertyName("minimumDuration")]
    public string? MinimumDuration { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    /// <summary>From <c>indexing.youTubePublicationOffset</c>, as the podcast DTO: <c>TimeSpan</c> "g" format, <c>""</c> when unset.</summary>
    [JsonPropertyName("youTubePublicationDelay")]
    public string YouTubePublicationDelay { get; set; } = "";

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }
}
