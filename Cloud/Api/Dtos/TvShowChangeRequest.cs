using System.Text.Json.Serialization;

namespace Api.Dtos;

public class TvShowChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }
}
