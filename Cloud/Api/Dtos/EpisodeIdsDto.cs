using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Platform episode ids. JSON-identical to the domain <c>EpisodeIds</c>.</summary>
public class EpisodeIdsDto
{
    [JsonPropertyName("spotify")]
    public string? Spotify { get; set; }

    [JsonPropertyName("apple")]
    public long? Apple { get; set; }

    [JsonPropertyName("youtube")]
    public string? YouTube { get; set; }
}
