using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Error body for discovery curation / supervised-discovery endpoints (400/403/404/500).</summary>
public class DiscoveryErrorResponse
{
    [JsonPropertyName("message")]
    [JsonPropertyOrder(10)]
    public string Message { get; set; } = "";
}

/// <summary>409 body from POST DiscoveryCuration: reports not available (not Unprocessed / already claimed).</summary>
public class DiscoveryUnavailableReportsResponse
{
    [JsonPropertyName("message")]
    [JsonPropertyOrder(10)]
    public string Message { get; set; } = "";

    [JsonPropertyName("documentIds")]
    [JsonPropertyOrder(20)]
    public Guid[] DocumentIds { get; set; } = [];
}

/// <summary>400 body from a supervised POST DiscoveryCuration: result ids not in the submitted reports.</summary>
public class DiscoveryUnknownResultIdsResponse
{
    [JsonPropertyName("message")]
    [JsonPropertyOrder(10)]
    public string Message { get; set; } = "";

    [JsonPropertyName("resultIds")]
    [JsonPropertyOrder(20)]
    public Guid[] ResultIds { get; set; } = [];
}
