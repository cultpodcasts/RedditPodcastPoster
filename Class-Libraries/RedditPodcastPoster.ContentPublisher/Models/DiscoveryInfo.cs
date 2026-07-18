using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class DiscoveryInfo
{
    [JsonPropertyName("documentCount")]
    [JsonPropertyOrder(1)]
    public int DocumentCount { get; set; }

    [JsonPropertyName("numberOfResults")]
    [JsonPropertyOrder(2)]
    public int? NumberOfResults { get; set; } = null;

    [JsonPropertyName("discoveryBegan")]
    [JsonPropertyOrder(3)]
    public DateTime? DiscoveryBegan { get; set; } = null;

    /// <summary>
    /// Watermark of the most recent successful Discover run. Survives curation clearing unprocessed docs.
    /// Used for dynamic lookback; distinct from <see cref="DiscoveryBegan"/> (min unprocessed queue start).
    /// </summary>
    [JsonPropertyName("lastSuccessfulDiscoveryBegan")]
    [JsonPropertyOrder(4)]
    public DateTime? LastSuccessfulDiscoveryBegan { get; set; } = null;
}