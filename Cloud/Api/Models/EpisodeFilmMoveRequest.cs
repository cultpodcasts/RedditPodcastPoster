namespace Api.Models;

public class EpisodeFilmMoveRequest
{
    /// <summary>Optional film name. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }

    /// <summary><c>select</c>, <c>continue</c>, or <c>cancel</c> after an ambiguous-identity prompt.</summary>
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    public string? SelectedIdentityKey { get; set; }
}
