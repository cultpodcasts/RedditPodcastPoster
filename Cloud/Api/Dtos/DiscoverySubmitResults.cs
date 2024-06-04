using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResults
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("errorsOccured")]
    public bool ErrorsOccurred { get; set; }

    [JsonPropertyName("results")]
    public DiscoveryItemResult[] Results { get; set; }
}

public class DiscoveryItemResult
{
    public Guid DiscoveryItemId { get; set; }
    public string Message { get; set; }
}