using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeTrailerAttachRequest
{
    [JsonPropertyName("filmId")]
    public Guid FilmId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>With <see cref="FilmName"/> instead of <see cref="FilmId"/>: create the film when no film has the name.</summary>
    [JsonPropertyName("createFilm")]
    public bool CreateFilm { get; set; }

    [JsonPropertyName("filmName")]
    public string? FilmName { get; set; }

    /// <summary>Answer to an ambiguous-identity prompt for a new film: <c>select</c>, <c>continue</c>, or <c>cancel</c>.</summary>
    [JsonPropertyName("identityDecision")]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    [JsonPropertyName("selectedIdentityKey")]
    public string? SelectedIdentityKey { get; set; }
}
