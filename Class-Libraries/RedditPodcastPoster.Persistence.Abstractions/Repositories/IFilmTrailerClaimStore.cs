using RedditPodcastPoster.Models.Films;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Decides which film an episode becomes a trailer of: one <see cref="FilmTrailerClaim"/> per episode in LookUps, written
/// only with conditional creates and etag-checked replaces, so two attaches can never both own the trailer.
/// </summary>
public interface IFilmTrailerClaimStore
{
    /// <summary>
    /// Claims <paramref name="episodeId"/> for <paramref name="filmId"/> with a conditional create. Claimed when the
    /// create succeeds or the existing claim already names this film; otherwise returns the holder and its etag.
    /// </summary>
    Task<FilmTrailerClaimAttempt> TryClaim(Guid episodeId, Guid filmId, DateTime nowUtc);

    /// <summary>
    /// Replaces a claim held by another film, with If-Match on <paramref name="holderETag"/>, so only one taker wins.
    /// False when the claim changed or vanished meanwhile.
    /// </summary>
    Task<bool> TryTakeOver(Guid episodeId, Guid filmId, DateTime nowUtc, string holderETag);
}

/// <param name="Claimed">True when the episode is claimed for the requested film.</param>
/// <param name="Holder">The claim of another film when not claimed.</param>
/// <param name="HolderETag">Etag of <paramref name="Holder"/>, for <see cref="IFilmTrailerClaimStore.TryTakeOver"/>.</param>
public sealed record FilmTrailerClaimAttempt(bool Claimed, FilmTrailerClaim? Holder = null, string? HolderETag = null);
