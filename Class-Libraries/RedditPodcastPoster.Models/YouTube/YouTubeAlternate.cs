using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.YouTube;

/// <summary>
/// An extra YouTube upload of the same catalogue item (RPP #1411). The primary upload stays on
/// <c>services.youtube</c> / <c>youTubeId</c> (or a podcast episode's <c>ids.youtube</c>). Alternates are an
/// ordered list: the first is the player fallback when the primary is unavailable. One YouTube video id may
/// belong to only one catalogue item, whether as its primary, an alternate, or a film trailer.
/// </summary>
public sealed class YouTubeAlternate
{
    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(1)]
    public required string YouTubeId { get; set; }

    [JsonPropertyName("url")]
    [JsonPropertyOrder(2)]
    public required Uri Url { get; set; }

    [JsonPropertyName("image")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Uri? Image { get; set; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; set; }

    [JsonPropertyName("channelId")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChannelId { get; set; }

    [JsonPropertyName("channelName")]
    [JsonPropertyOrder(6)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChannelName { get; set; }

    /// <summary>When this upload was published (UTC).</summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(7)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? Release { get; set; }

    [JsonPropertyName("duration")]
    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TimeSpan? Length { get; set; }

    /// <summary>The podcast episode this upload was attached from, when it came through the curator route.</summary>
    [JsonPropertyName("sourceEpisodeId")]
    [JsonPropertyOrder(9)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? SourceEpisodeId { get; set; }

    [JsonPropertyName("addedAt")]
    [JsonPropertyOrder(10)]
    public DateTime AddedAt { get; set; }
}
