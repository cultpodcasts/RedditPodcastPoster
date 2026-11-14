using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Command outcome for POST episode/{podcastId}/{episodeId}/trailer. The client GETs the film for its read model.</summary>
public sealed class EpisodeTrailerAttachResponse
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

    /// <summary>True when the request had <c>createFilm</c> and this call created the film.</summary>
    [JsonPropertyName("filmCreated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FilmCreated { get; init; }

    /// <summary>
    /// With <c>createFilm</c>: set when the new film's identity is ambiguous; nothing was written. Re-POST with
    /// <c>identityDecision</c> and <c>selectedIdentityKey</c> (same shape as Move to film).
    /// </summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubmitUrlResponse.IdentityDecisionBody? IdentityDecision { get; init; }

    /// <summary>True when the curator cancelled the identity prompt; nothing was written.</summary>
    [JsonPropertyName("cancelled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Cancelled { get; init; }
}
