using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoveryIngest
{
    [JsonPropertyName("ids")]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    [JsonPropertyName("urls")]
    public Uri[] Urls { get; set; } = [];
}