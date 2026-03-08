using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.V2;

public class Episode
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(2)]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(10)]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(20)]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("release")]
    [JsonPropertyOrder(30)]
    public DateTime Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(31)]
    public TimeSpan Length { get; set; }

    [JsonPropertyName("explicit")]
    [JsonPropertyOrder(32)]
    public bool Explicit { get; set; }

    [JsonPropertyName("posted")]
    [JsonPropertyOrder(40)]
    public bool Posted { get; set; }

    [JsonPropertyName("tweeted")]
    [JsonPropertyOrder(41)]
    public bool Tweeted { get; set; }

    [JsonPropertyName("bluesky")]
    [JsonPropertyOrder(42)]
    public bool? BlueskyPosted { get; set; }

    [JsonPropertyName("ignored")]
    [JsonPropertyOrder(43)]
    public bool Ignored { get; set; }

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(44)]
    public bool Removed { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(50)]
    public string SpotifyId { get; set; } = string.Empty;

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(51)]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(52)]
    public string YouTubeId { get; set; } = string.Empty;

    [JsonPropertyName("urls")]
    [JsonPropertyOrder(60)]
    public ServiceUrls Urls { get; set; } = new();

    [JsonPropertyName("subjects")]
    [JsonPropertyOrder(70)]
    public List<string> Subjects { get; set; } = [];

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(80)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("podcastName")]
    [JsonPropertyOrder(90)]
    public string? PodcastName { get; set; }

    [JsonPropertyName("podcastSearchTerms")]
    [JsonPropertyOrder(91)]
    public string? PodcastSearchTerms { get; set; }

    [JsonPropertyName("searchLang")]
    [JsonPropertyOrder(92)]
    public string? SearchLanguage { get; set; }

    [JsonPropertyName("podcastMetadataVersion")]
    [JsonPropertyOrder(93)]
    public long? PodcastMetadataVersion { get; set; }

    [JsonPropertyName("images")]
    [JsonPropertyOrder(150)]
    public EpisodeImages? Images { get; set; }

    [JsonPropertyName("twitterHandles")]
    [JsonPropertyOrder(160)]
    public string[]? TwitterHandles { get; set; }

    [JsonPropertyName("blueskyHandles")]
    [JsonPropertyOrder(161)]
    public string[]? BlueskyHandles { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }
}
