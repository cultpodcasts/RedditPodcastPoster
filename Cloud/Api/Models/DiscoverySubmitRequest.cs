using System.Text.Json.Serialization;

namespace Api.Models;

public class DiscoverySubmitRequest
{
    [JsonPropertyName("ids")]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    [JsonPropertyName("resultIds")]
    public Guid[] ResultIds { get; set; } = [];
}