using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>PATCH body for <c>tvshowepisode/{id}</c>: canonical ids plus the shared playable fields.</summary>
public class TvShowEpisodeChangeRequest : PlayableFieldsChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }
}
