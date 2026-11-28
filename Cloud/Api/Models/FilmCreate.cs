using RedditPodcastPoster.Catalogue.Authority;

namespace Api.Models;

/// <summary>
/// Create a film from its TMDB / IMDb identity (RPP #1413), without moving an episode.
/// Either identity ids (<see cref="TmdbId"/> / <see cref="ImdbId"/>), or the identity-prompt shape
/// (<see cref="Title"/> with <see cref="IdentityDecision"/> / <see cref="SelectedIdentityKey"/>, as Move to film).
/// </summary>
public class FilmCreateRequest
{
    public int? TmdbId { get; set; }

    /// <summary>IMDb title id, <c>tt</c> then digits.</summary>
    public string? ImdbId { get; set; }

    /// <summary>The film name. With ids it overrides the identity title; without ids it is the title to look up.</summary>
    public string? Title { get; set; }

    /// <summary>Optional publisher / distributor. Overrides the identity's publisher.</summary>
    public string? Publisher { get; set; }

    /// <summary><c>select</c>, <c>continue</c>, or <c>cancel</c> after an ambiguous-identity prompt.</summary>
    public string? IdentityDecision { get; set; }

    /// <summary>Candidate <c>key</c> when <see cref="IdentityDecision"/> is <c>select</c> (e.g. <c>tmdb:123</c>).</summary>
    public string? SelectedIdentityKey { get; set; }
}

public record FilmCreateCommand(FilmCreateRequest Request);

public enum FilmCreateStatus
{
    /// <summary>The film was created and indexed.</summary>
    Accepted,

    /// <summary>The request is invalid (no identity or title, a bad id, a name containing '/').</summary>
    BadRequest,

    /// <summary>No film matches the given TMDB / IMDb id.</summary>
    IdentityNotFound,

    /// <summary>A film already has the identity or the name. <c>FilmId</c> is that film; nothing was written.</summary>
    Conflict,

    /// <summary>
    /// Authority search is ambiguous; nothing was created. 200 with <c>identityDecision</c>. Re-POST with
    /// <c>identityDecision</c> + <c>selectedIdentityKey</c> (or <c>continue</c> / <c>cancel</c>).
    /// </summary>
    IdentityDecisionRequired,

    /// <summary>Curator cancelled the identity prompt; nothing was created.</summary>
    Cancelled,

    /// <summary>A write or the search push failed. With <c>FailureIndexing</c> the film exists but is not indexed.</summary>
    Failed
}

public record FilmCreateResult(
    FilmCreateStatus Status,
    Guid? FilmId = null,
    string? Name = null,
    string? Error = null,
    bool FailureIndexing = false,
    CatalogueIdentityHold? IdentityHold = null);
