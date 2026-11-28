using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>Body of POST film (curate). See docs/create-film-from-identity.md.</summary>
public class FilmCreateRequest
{
    [JsonPropertyName("tmdbId")]
    public int? TmdbId { get; set; }

    [JsonPropertyName("imdbId")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("publisher")]
    public string? Publisher { get; set; }

    /// <summary>Answer to an ambiguous-identity prompt: <c>select</c>, <c>continue</c>, or <c>cancel</c>.</summary>
    [JsonPropertyName("identityDecision")]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    [JsonPropertyName("selectedIdentityKey")]
    public string? SelectedIdentityKey { get; set; }
}
