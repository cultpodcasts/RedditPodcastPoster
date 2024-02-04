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

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(21)]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    [JsonPropertyOrder(30)]
    public bool Bundles { get; set; } = false;

    [JsonPropertyName("indexAllEpisodes")]
    [JsonPropertyOrder(31)]
    public bool IndexAllEpisodes { get; set; } = false;

    [JsonPropertyName("bypassShortEpisodeChecking")]
    [JsonPropertyOrder(32)]
    public bool? BypassShortEpisodeChecking { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonPropertyOrder(40)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonPropertyOrder(41)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(50)]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("spotifyMarket")]
    [JsonPropertyOrder(51)]
    public string? SpotifyMarket { get; set; }

    [JsonPropertyName("spotifyEpisodesQueryIsExpensive")]
    [JsonPropertyOrder(52)]
    public bool? SpotifyEpisodesQueryIsExpensive { get; set; }

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(60)]
    public long? AppleId { get; set; } = null;

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(70)]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistId")]
    [JsonPropertyOrder(71)]
    public string YouTubePlaylistId { get; set; } = "";

    [JsonPropertyName("youTubePublicationDelay")]
    [JsonPropertyOrder(72)]
    public string YouTubePublishingDelayTimeSpan { get; set; } = "";

    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    [JsonPropertyOrder(73)]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    [JsonPropertyOrder(74)]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("youTubeNotificationSubscriptionLeaseExpiry")]
    [JsonPropertyOrder(75)]
    public DateTime? YouTubeNotificationSubscriptionLeaseExpiry { get; set; }

    [JsonPropertyName("twitterHandle")]
    [JsonPropertyOrder(80)]
    public string TwitterHandle { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    [JsonPropertyOrder(90)]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    [JsonPropertyOrder(91)]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    [JsonPropertyOrder(92)]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    [JsonPropertyOrder(93)]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

    [JsonPropertyName("ignoredAssociatedSubjects")]
    [JsonPropertyOrder(93)]
    public string[]? IgnoredAssociatedSubjects { get; set; }

    [JsonPropertyName("ignoredSubjects")]
    [JsonPropertyOrder(93)]
    public string[]? IgnoredSubjects { get; set; }


    [JsonPropertyName("defaultSubject")]
    [JsonPropertyOrder(94)]
    public string? DefaultSubject { get; set; }

    [JsonPropertyName("episodes")]
    [JsonPropertyOrder(200)]
    public List<Episode> Episodes { get; set; } = new();

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
        if (string.IsNullOrEmpty(YouTubePublishingDelayTimeSpan))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.Parse(YouTubePublishingDelayTimeSpan);
    }
}