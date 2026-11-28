using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>
/// Command outcome for POST film. The client GETs the film (<c>film/{filmId}</c>) for its read model.
/// Ambiguous identity returns 200 with <see cref="IdentityDecision"/> and no film created. A 409 carries the
/// <see cref="FilmId"/> of the film that already has the identity or name.
/// </summary>
public sealed class FilmCreateResponse
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

    /// <summary>Set when authority search is ambiguous; nothing was created.</summary>
    [JsonPropertyName("identityDecision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SubmitUrlResponse.IdentityDecisionBody? IdentityDecision { get; init; }

    /// <summary>True when the curator cancelled the identity prompt; nothing was created.</summary>
    [JsonPropertyName("cancelled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Cancelled { get; init; }
}
