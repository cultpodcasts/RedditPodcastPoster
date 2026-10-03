using System.Text.Json.Serialization;

namespace Api.Models;

public class TvShowEpisodeChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }

    [JsonPropertyName("tvdb")]
    public string? Tvdb { get; set; }
}
