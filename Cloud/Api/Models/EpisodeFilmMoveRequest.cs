namespace Api.Models;

public class EpisodeFilmMoveRequest
{
    /// <summary>Optional film name. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }

    /// <summary><c>select</c>, <c>continue</c>, or <c>cancel</c> after an ambiguous-identity prompt.</summary>
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    public string? SelectedIdentityKey { get; set; }

    /// <summary>
    /// RPP #1522 Move to an existing film: the film to merge the episode into. Null (or empty) creates a new film from
    /// the episode as before; when set, <see cref="Title"/> and the identity fields are ignored.
    /// </summary>
    public Guid? FilmId { get; set; }
}
