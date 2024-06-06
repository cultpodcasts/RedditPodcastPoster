using System.Text.Json.Serialization;

namespace Api.Dtos.Extensions;

public class MatchingPodcast
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public required Guid Id { get; set; }

    [JsonPropertyName("name")]
    [JsonPropertyOrder(20)]
    public required string Name { get; set; }
}