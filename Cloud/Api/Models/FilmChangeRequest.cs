using System.Text.Json.Serialization;

namespace Api.Models;

public class FilmChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }
}
