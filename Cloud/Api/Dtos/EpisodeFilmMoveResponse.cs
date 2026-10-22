using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Command outcome for POST episode/{podcastId}/{episodeId}/film. The client GETs the film for its read model.</summary>
public sealed class EpisodeFilmMoveResponse
{
    [JsonPropertyName("filmId")]
    public Guid? FilmId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    [JsonPropertyName("failureIndexing")]
    public bool FailureIndexing { get; init; }
}
