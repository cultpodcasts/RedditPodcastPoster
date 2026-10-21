using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>PATCH body for <c>tvshow/{id}</c>: canonical ids plus the shared publisher fields.</summary>
public class TvShowChangeRequest : PublisherFieldsChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }
}
