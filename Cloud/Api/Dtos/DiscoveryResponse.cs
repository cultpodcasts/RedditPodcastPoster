using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoveryResponse
{
    [JsonPropertyName("ids")]
    public required IEnumerable<Guid> Ids { get; set; }

    [JsonPropertyName("results")]
    public required IEnumerable<DiscoveryResponseItem> Results { get; set; }
}