namespace Api.Models;

public class EpisodeTrailerAttachRequest
{
    /// <summary>The film that takes the episode as a trailer. Empty when <see cref="CreateFilm"/> is set.</summary>
    public Guid FilmId { get; set; }

    /// <summary>
    /// Attach to the film named <see cref="FilmName"/> instead of <see cref="FilmId"/>, creating it when no film has
    /// the name (an unreleased film: no release, no services). When one film already has the name the trailer joins it.
    /// </summary>
    public bool CreateFilm { get; set; }

    /// <summary>Film name used with <see cref="CreateFilm"/>.</summary>
    public string? FilmName { get; set; }

    /// <summary><c>select</c>, <c>continue</c>, or <c>cancel</c> after an ambiguous-identity prompt for a new film.</summary>
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    public string? SelectedIdentityKey { get; set; }

    /// <summary>Optional trailer title. Null or blank keeps the episode title.</summary>
    public string? Title { get; set; }
}
