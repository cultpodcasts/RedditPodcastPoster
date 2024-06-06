using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResponse
{
    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("errorsOccured")]
    public bool ErrorsOccurred { get; set; }

    [JsonPropertyName("results")]
    public required DiscoverySubmitResponseItem[] Results { get; set; }
}