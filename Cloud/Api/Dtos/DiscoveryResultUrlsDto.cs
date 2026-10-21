using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Discovery result URLs. JSON-identical to the domain <c>DiscoveryResultUrls</c>.</summary>
public class DiscoveryResultUrlsDto
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(10)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(20)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(30)]
    public Uri? YouTube { get; set; }
}
