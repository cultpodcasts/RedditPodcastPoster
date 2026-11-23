using System.Text.Json.Serialization;

namespace Api.Dtos;

public class EpisodeFilmMoveRequest
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Answer to an ambiguous-identity prompt: <c>select</c>, <c>continue</c>, or <c>cancel</c>.</summary>
    [JsonPropertyName("identityDecision")]
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    [JsonPropertyName("selectedIdentityKey")]
    public string? SelectedIdentityKey { get; set; }

    /// <summary>
    /// Move to an existing film (RPP #1522): the film to merge the episode into. The episode's links the film has no
    /// link for are added to it, the episode leaves search and its short link redirects to the film. Omit to create a
    /// new film. Podcast episodes only; a news report move with <c>filmId</c> is a 400.
    /// </summary>
    [JsonPropertyName("filmId")]
    public Guid? FilmId { get; set; }
}
