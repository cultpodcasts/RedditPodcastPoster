using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoveryAwaitingReviewDto
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public required Guid Id { get; set; }

    [JsonPropertyName("discoveryBegan")]
    [JsonPropertyOrder(20)]
    public required DateTime DiscoveryBegan { get; set; }

    [JsonPropertyName("resultCount")]
    [JsonPropertyOrder(30)]
    public required int ResultCount { get; set; }
}
