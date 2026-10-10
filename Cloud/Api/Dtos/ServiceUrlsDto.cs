using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Platform episode URLs. JSON-identical to the domain <c>ServiceUrls</c>.</summary>
public class ServiceUrlsDto
{
    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(1)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(2)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(3)]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("internetArchive")]
    [JsonPropertyOrder(4)]
    public Uri? InternetArchive { get; set; }

    [JsonPropertyName("bbc")]
    [JsonPropertyOrder(5)]
    public Uri? BBC { get; set; }
}
