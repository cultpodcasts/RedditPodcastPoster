using System.Text.Json.Serialization;

namespace Api.Dtos;

public class FilmChangeRequest
{
    [JsonPropertyName("imdb")]
    public string? Imdb { get; set; }
}
