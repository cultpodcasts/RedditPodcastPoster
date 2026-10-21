using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeFilmMoveRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
