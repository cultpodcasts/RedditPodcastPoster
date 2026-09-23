using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Podcasts;

[CosmosSelector(ModelType.Podcast)]
public class Podcast : Publisher
{
    [JsonIgnore]
    public static readonly RegexOptions DescriptionFlags = RegexOptions.IgnoreCase | RegexOptions.Singleline;

    [JsonIgnore]
    public static readonly RegexOptions TitleFlags = RegexOptions.IgnoreCase;

    [JsonIgnore]
    public static readonly RegexOptions EpisodeMatchFlags = RegexOptions.Compiled;

    [JsonIgnore]
    public static readonly RegexOptions EpisodeIncludeTitleFlags = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    public Podcast()
    {
        ModelType = ModelType.Podcast;
    }

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(40)]
    public bool Bundles { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    [JsonPropertyOrder(50)]
    public bool IndexAllEpisodes { get; set; }

    [JsonPropertyName("ignoreAllEpisodes")]
    [JsonPropertyOrder(51)]
    public bool? IgnoreAllEpisodes { get; set; }

    [JsonPropertyName("bypassShortEpisodeChecking")]
    [JsonPropertyOrder(60)]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("alwaysPromoteAsHero")]
    [JsonPropertyOrder(62)]
    public bool? AlwaysPromoteAsHero { get; set; }

    [JsonPropertyName("minimumDuration")]
    [JsonPropertyOrder(61)]
    public TimeSpan? MinimumDuration { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonPropertyOrder(70)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonPropertyOrder(80)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(90)]
    public string SpotifyId { get; set; } = string.Empty;

    [JsonPropertyName("spotifyMarket")]
    [JsonPropertyOrder(100)]
    public string? SpotifyMarket { get; set; }

    [JsonPropertyName("spotifyEpisodesQueryIsExpensive")]
    [JsonPropertyOrder(110)]
    public bool? SpotifyEpisodesQueryIsExpensive { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(120)]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(130)]
    public string YouTubeChannelId { get; set; } = string.Empty;

    [JsonPropertyName("youTubePlaylistId")]
    [JsonPropertyOrder(140)]
    public string YouTubePlaylistId { get; set; } = string.Empty;

    /// <summary>
    /// Former YouTube playlist ids (id + UTC replaced-at), newest last. Populated when
    /// <see cref="YouTubePlaylistId"/> changes through the playlist-id change helper.
    /// </summary>
    [JsonPropertyName("youTubePlaylistIdHistory")]
    [JsonPropertyOrder(141)]
    public List<YouTubePlaylistIdHistoryEntry>? YouTubePlaylistIdHistory { get; set; }

    /// <summary>
    /// Declared playlist ordering. Null: probe head order each pass (default). Arbitrary: manually
    /// curated playlist where position carries no date information — full walk + added-at filter.
    /// </summary>
    [JsonPropertyName("youTubePlaylistOrder")]
    [JsonPropertyOrder(150)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PlaylistOrder? YouTubePlaylistOrder { get; set; }

    [JsonPropertyName("youTubePublicationOffset")]
    [JsonPropertyOrder(151)]
    public long? YouTubePublicationOffset { get; set; }

    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    [JsonPropertyOrder(160)]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    [JsonPropertyName("youTubeChannelSearchForbidden")]
    [JsonPropertyOrder(161)]
    public bool? YouTubeChannelSearchForbidden { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    [JsonPropertyOrder(170)]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("youTubeNotificationSubscriptionLeaseExpiry")]
    [JsonPropertyOrder(180)]
    public DateTime? YouTubeNotificationSubscriptionLeaseExpiry { get; set; }

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(200)]
    public string TitleRegex { get; set; } = string.Empty;

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(210)]
    public string DescriptionRegex { get; set; } = string.Empty;

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(220)]
    public string EpisodeMatchRegex { get; set; } = string.Empty;

    [JsonPropertyName("episodeIncludeTitleRegex")]
    [JsonPropertyOrder(230)]
    public string EpisodeIncludeTitleRegex { get; set; } = string.Empty;

    public bool HasExpensiveYouTubePlaylistQuery()
    {
        return YouTubePlaylistQueryIsExpensive.HasValue && YouTubePlaylistQueryIsExpensive.Value;
    }

    public bool HasArbitraryYouTubePlaylistOrder()
    {
        return YouTubePlaylistOrder == PlaylistOrder.Arbitrary;
    }

    public bool HasYouTubeChannelSearchForbidden()
    {
        return YouTubeChannelSearchForbidden.HasValue && YouTubeChannelSearchForbidden.Value;
    }

    public bool HasExpensiveSpotifyEpisodesQuery()
    {
        return SpotifyEpisodesQueryIsExpensive.HasValue && SpotifyEpisodesQueryIsExpensive.Value;
    }

    public TimeSpan YouTubePublishingDelay()
    {
        if (YouTubePublicationOffset == null)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(YouTubePublicationOffset.Value);
    }

    public bool HasIgnoreAllEpisodes()
    {
        return IgnoreAllEpisodes.HasValue && IgnoreAllEpisodes.Value;
    }
}
