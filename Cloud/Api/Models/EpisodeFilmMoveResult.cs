using RedditPodcastPoster.Catalogue.Authority;

namespace Api.Models;

public enum EpisodeFilmMoveStatus
{
    Accepted,
    NotFound,
    /// <summary>A film already has the episode's id, or another film already has the name.</summary>
    Conflict,
    /// <summary>The title is invalid (contains '/'), or the episode is removed.</summary>
    BadRequest,
    Failed,
    /// <summary>
    /// Authority search is ambiguous; nothing was created. 200 with <c>identityDecision</c>
    /// (same shape as URL-submit / Identity-tab lookup). Re-POST with <c>identityDecision</c>
    /// + <c>selectedIdentityKey</c> (or <c>continue</c> / <c>cancel</c>).
    /// </summary>
    IdentityDecisionRequired,
    /// <summary>Curator cancelled the identity prompt; nothing was created.</summary>
    Cancelled
}

public record EpisodeFilmMoveResult(
    EpisodeFilmMoveStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false,
    CatalogueIdentityHold? IdentityHold = null);
