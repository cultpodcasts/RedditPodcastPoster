using System.Text.Json.Serialization;

namespace CultPodcasts.DatabasePublisher.PublicModels;

/// <summary>One extra YouTube upload of a public catalogue item (RPP #1411).</summary>
public sealed class PublicYouTubeAlternate
{
    [JsonPropertyName("youTubeId")]
    [JsonPropertyOrder(1)]
    public required string YouTubeId { get; init; }

    [JsonPropertyName("url")]
    [JsonPropertyOrder(2)]
    public required Uri Url { get; init; }

    [JsonPropertyName("title")]
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("channelName")]
    [JsonPropertyOrder(4)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChannelName { get; init; }

    /// <summary>Publish date <c>yyyy-MM-dd</c> (UTC).</summary>
    [JsonPropertyName("release")]
    [JsonPropertyOrder(5)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Release { get; init; }
}
