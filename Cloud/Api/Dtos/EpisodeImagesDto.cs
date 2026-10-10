using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Episode artwork per platform. JSON-identical to the domain <c>EpisodeImages</c>.</summary>
public class EpisodeImagesDto
{
    [JsonPropertyName("youtube")]
    [JsonPropertyOrder(1)]
    public Uri? YouTube { get; set; }

    [JsonPropertyName("spotify")]
    [JsonPropertyOrder(3)]
    public Uri? Spotify { get; set; }

    [JsonPropertyName("apple")]
    [JsonPropertyOrder(3)]
    public Uri? Apple { get; set; }

    [JsonPropertyName("other")]
    [JsonPropertyOrder(4)]
    public Uri? Other { get; set; }
}
