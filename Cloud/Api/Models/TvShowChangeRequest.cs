using System.Text.Json.Serialization;

namespace Api.Models;

public class TvShowChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }
}
