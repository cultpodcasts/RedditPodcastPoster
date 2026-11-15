using RedditPodcastPoster.Catalogue.Authority;

namespace Api.Models;

public enum EpisodeTrailerAttachStatus
{
    Accepted,
    /// <summary>The podcast, episode, or film does not exist.</summary>
    NotFound,
    /// <summary>The episode or film is removed, the episode has no link, or the episode is the film.</summary>
    BadRequest,
    /// <summary>Another film already holds this episode as a trailer (<c>FilmId</c> is that film).</summary>
    Conflict,
    Failed,
    /// <summary>With <c>createFilm</c>: the new film's identity is ambiguous; nothing was written (200 with candidates).</summary>
    IdentityDecisionRequired,
    /// <summary>With <c>createFilm</c>: the curator cancelled the identity prompt; nothing was written.</summary>
    Cancelled
}

public record EpisodeTrailerAttachResult(
    EpisodeTrailerAttachStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false,
    bool FilmCreated = false,
    CatalogueIdentityHold? IdentityHold = null);
