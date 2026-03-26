using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Models.V2;

public class Podcast
{
    [JsonIgnore]
    public static readonly RegexOptions DescriptionFlags = RegexOptions.IgnoreCase | RegexOptions.Singleline;

    [JsonIgnore]
    public static readonly RegexOptions TitleFlags = RegexOptions.IgnoreCase;

    [JsonIgnore]
    public static readonly RegexOptions EpisodeMatchFlags = RegexOptions.Compiled;

    [JsonIgnore]
    public static readonly RegexOptions EpisodeIncludeTitleFlags = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("latestReleased")]
    [JsonPropertyOrder(21)]
    public DateTime? LatestReleased { get; set; }


    [JsonPropertyName("lang")]
    [JsonPropertyOrder(22)]
    public string? Language { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(25)]
    public bool? Removed { get; set; }

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(30)]
    public string Publisher { get; set; } = string.Empty;

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

    [JsonPropertyName("youTubePublicationOffset")]
    [JsonPropertyOrder(151)]
    public long? YouTubePublicationOffset { get; set; }

    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    [JsonPropertyOrder(160)]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    [JsonPropertyOrder(170)]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("youTubeNotificationSubscriptionLeaseExpiry")]
    [JsonPropertyOrder(180)]
    public DateTime? YouTubeNotificationSubscriptionLeaseExpiry { get; set; }

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(190)]
    public string TwitterHandle { get; set; } = string.Empty;

    [JsonPropertyName("blueskyHandle")]
    [JsonPropertyOrder(191)]
    public string? BlueskyHandle { get; set; }

    [JsonPropertyName("hashtag")]
    [JsonPropertyOrder(195)]
    public string? HashTag { get; set; }

    [JsonPropertyName("enrichmentHashTags")]
    [JsonPropertyOrder(196)]
    public string[]? EnrichmentHashTags { get; set; }

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

    [JsonPropertyName("ignoredAssociatedSubjects")]
    [JsonPropertyOrder(240)]
    public string[]? IgnoredAssociatedSubjects { get; set; }

    [JsonPropertyName("ignoredSubjects")]
    [JsonPropertyOrder(250)]
    public string[]? IgnoredSubjects { get; set; }

    [JsonPropertyName("defaultSubject")]
    [JsonPropertyOrder(260)]
    public string? DefaultSubject { get; set; }

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(270)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("knownTerms")]
    [JsonPropertyOrder(271)]
    public string[]? KnownTerms { get; set; }

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(290)]
    public string FileKey { get; set; } = string.Empty;

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

    public bool HasExpensiveYouTubePlaylistQuery()
    {
        return YouTubePlaylistQueryIsExpensive.HasValue && YouTubePlaylistQueryIsExpensive.Value;
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

    public bool IsRemoved()
    {
        return Removed.HasValue && Removed.Value;
    }

    public bool HasIgnoreAllEpisodes()
    {
        return IgnoreAllEpisodes.HasValue && IgnoreAllEpisodes.Value;
    }
}
