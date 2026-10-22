using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeTrailerAttachRequest
{
    [JsonPropertyName("filmId")]
    public Guid FilmId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
