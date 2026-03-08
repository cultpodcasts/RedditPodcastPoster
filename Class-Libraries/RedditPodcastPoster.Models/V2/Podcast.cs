using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.V2;

public class Podcast
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(10)]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lang")]
    [JsonPropertyOrder(20)]
    public string? Language { get; set; }

    [JsonPropertyName("publisher")]
    [JsonPropertyOrder(30)]
    public string Publisher { get; set; } = string.Empty;

    [JsonPropertyName("removed")]
    [JsonPropertyOrder(40)]
    public bool? Removed { get; set; }

    [JsonPropertyName("searchTerms")]
    [JsonPropertyOrder(50)]
    public string? SearchTerms { get; set; }

    [JsonPropertyName("spotifyId")]
    [JsonPropertyOrder(60)]
    public string SpotifyId { get; set; } = string.Empty;

    [JsonPropertyName("appleId")]
    [JsonPropertyOrder(70)]
    public long? AppleId { get; set; }

    [JsonPropertyName("youTubeChannelId")]
    [JsonPropertyOrder(80)]
    public string YouTubeChannelId { get; set; } = string.Empty;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(90)]
    public string FileKey { get; set; } = string.Empty;

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }
}
