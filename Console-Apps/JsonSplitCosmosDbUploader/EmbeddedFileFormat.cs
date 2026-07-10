using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;

namespace JsonSplitCosmosDbUploader.EmbeddedFileFormat;

/// <summary>
/// Deserialises legacy single-container podcast JSON files with embedded episodes.
/// </summary>
[CosmosSelector(ModelType.Podcast)]
public sealed class EmbeddedPodcast : CosmosSelector
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("lang")]
    public string? Language { get; set; }

    [JsonPropertyName("publisher")]
    public string Publisher { get; set; } = "";

    [JsonPropertyName("hasBundledEpisodes")]
    public bool Bundles { get; set; }

    [JsonPropertyName("indexAllEpisodes")]
    public bool IndexAllEpisodes { get; set; }

    [JsonPropertyName("releaseAuthority")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? ReleaseAuthority { get; set; }

    [JsonPropertyName("primaryPostService")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Service? PrimaryPostService { get; set; }

    [JsonPropertyName("spotifyId")]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("spotifyMarket")]
    public string? SpotifyMarket { get; set; }

    [JsonPropertyName("spotifyEpisodesQueryIsExpensive")]
    public bool? SpotifyEpisodesQueryIsExpensive { get; set; }

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeChannelId")]
    public string YouTubeChannelId { get; set; } = "";

    [JsonPropertyName("youTubePlaylistId")]
    public string YouTubePlaylistId { get; set; } = "";

    [JsonPropertyName("youTubePublicationOffset")]
    public long? YouTubePublicationOffset { get; set; }

    [JsonPropertyName("youTubePlaylistQueryIsExpensive")]
    public bool? YouTubePlaylistQueryIsExpensive { get; set; }

    [JsonPropertyName("skipEnrichingFromYouTube")]
    public bool? SkipEnrichingFromYouTube { get; set; }

    [JsonPropertyName("twitterHandle")]
    public string TwitterHandle { get; set; } = "";

    [JsonPropertyName("titleRegex")]
    public string TitleRegex { get; set; } = "";

    [JsonPropertyName("descriptionRegex")]
    public string DescriptionRegex { get; set; } = "";

    [JsonPropertyName("episodeMatchRegex")]
    public string EpisodeMatchRegex { get; set; } = "";

    [JsonPropertyName("episodeIncludeTitleRegex")]
    public string EpisodeIncludeTitleRegex { get; set; } = "";

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("episodes")]
    public List<EmbeddedEpisode> Episodes { get; set; } = [];
}

public class EmbeddedEpisode
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("lang")]
    public string? Language { get; set; }

    [JsonPropertyName("posted")]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    public bool Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    public bool Removed { get; set; }

    [JsonPropertyName("release")]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    public bool Explicit { get; set; }

    [JsonPropertyName("spotifyId")]
    public string SpotifyId { get; set; } = "";

    [JsonPropertyName("appleId")]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeId")]
    public string YouTubeId { get; set; } = "";

    [JsonPropertyName("urls")]
    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("images")]
    public EpisodeImages? Images { get; set; }
}
