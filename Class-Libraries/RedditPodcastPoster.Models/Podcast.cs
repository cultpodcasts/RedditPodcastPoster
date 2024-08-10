using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models;

[CosmosSelector(ModelType.Podcast)]
public sealed class Podcast : CosmosSelector
{
    public Podcast(Guid id)
    {
        Id = id;
        ModelType = ModelType.Podcast;
    }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public string Name { get; set; } = "";

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(21)]
    public bool? Removed { get; set; }

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(30)]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(40)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("indexAllEpisodes")]
    [JsonPropertyOrder(50)]
    public bool IndexAllEpisodes { get; set; } = false;

    [JsonPropertyName("bypassShortEpisodeChecking")]
    [JsonPropertyOrder(60)]
    public bool? BypassShortEpisodeChecking { get; set; }

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
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("spotifyMarket")]
    [JsonPropertyOrder(100)]
    public string? SpotifyMarket { get; set; }

    [JsonPropertyName("spotifyEpisodesQueryIsExpensive")]
    [JsonPropertyOrder(110)]
    public bool? SpotifyEpisodesQueryIsExpensive { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(120)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(130)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistId")]
    [JsonPropertyOrder(140)]
    public string YouTubePlaylistId { get; set; } = "";

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
    public string TwitterHandle { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(200)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(210)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(220)]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    [JsonPropertyOrder(230)]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

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

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(280)]
    public List<Episode> Episodes { get; set; } = [];

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
        if (YouTubePublicationOffset==null)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromTicks(YouTubePublicationOffset.Value);
    }

    public bool IsRemoved()
    {
        return Removed.HasValue && Removed.Value;
    }
}