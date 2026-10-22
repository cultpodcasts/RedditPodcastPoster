using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeTvShowMoveRequest
{
    [JsonPropertyName("tvShowId")]
    public Guid? TvShowId { get; set; }

    [JsonPropertyName("tvShowName")]
    public string? TvShowName { get; set; }

    [JsonPropertyName("createShow")]
    public bool? CreateShow { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}
