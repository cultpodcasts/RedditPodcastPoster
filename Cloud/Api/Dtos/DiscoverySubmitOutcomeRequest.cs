using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Body for POST DiscoveryCuration/submit-outcome — same id shape as submit.</summary>
public class DiscoverySubmitOutcomeRequest
{
    [JsonPropertyName("ids")]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    [JsonPropertyName("resultIds")]
    public Guid[] ResultIds { get; set; } = [];
}
