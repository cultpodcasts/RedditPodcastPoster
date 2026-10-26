using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome for POST episode/{podcastId}/{episodeId}/film. The client GETs the film for its read model.
/// Ambiguous identity returns 200 with <see cref="IdentityDecision"/> and no film created (same as URL-submit).
/// </summary>
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

    /// <summary>Set when authority search is ambiguous; nothing was created. Re-use send-podcast / Identity-tab UI.</summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubmitUrlResponse.IdentityDecisionBody? IdentityDecision { get; init; }

    /// <summary>True when the curator cancelled the identity prompt; nothing was created.</summary>
    [JsonPropertyName("cancelled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Cancelled { get; init; }
}
