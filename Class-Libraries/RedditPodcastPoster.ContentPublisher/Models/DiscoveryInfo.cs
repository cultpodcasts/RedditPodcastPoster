using System.Text.Json.Serialization;

namespace RedditPodcastPoster.ContentPublisher.Models;

public class DiscoveryInfo
{
    [JsonPropertyName("documentCount")]
    [JsonPropertyOrder(1)]
    public int DocumentCount { get; set; }

    [JsonPropertyName("numberOfResults")]
    [JsonPropertyOrder(2)]
    public int NumberOfResults { get; set; }

    [JsonPropertyName("discoveryBegan")]
    [JsonPropertyOrder(3)]
    public DateTime DiscoveryBegan { get; set; }
}