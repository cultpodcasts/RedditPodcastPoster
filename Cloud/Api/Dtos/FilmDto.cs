using System.Text.Json.Serialization;

namespace Api.Dtos;

public class FilmDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("imdb")]
    public Uri? Imdb { get; set; }
}
