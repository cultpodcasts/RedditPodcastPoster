using System.Text.Json.Serialization;

namespace Api.Dtos;

public class DiscoverySubmitResponseItem
{
    [JsonPropertyName("discoveryItemId")]
    public required Guid DiscoveryItemId { get; set; }

    [JsonPropertyName("episodeId")]
    public Guid? EpisodeId { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }
}