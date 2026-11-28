using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Decides which TV show an episode becomes a trailer of: one <see cref="TvShowTrailerClaim"/> per episode in LookUps,
/// written only with conditional creates and etag-checked replaces, so two attaches can never both own the trailer.
/// </summary>
public interface ITvShowTrailerClaimStore
{
    /// <summary>
    /// Claims <paramref name="episodeId"/> for <paramref name="tvShowId"/> with a conditional create. Claimed when the
    /// create succeeds or the existing claim already names this show; otherwise returns the holder and its etag.
    /// </summary>
    Task<TvShowTrailerClaimAttempt> TryClaim(Guid episodeId, Guid tvShowId, DateTime nowUtc);

    /// <summary>
    /// Replaces a claim held by another show, with If-Match on <paramref name="holderETag"/>, so only one taker wins.
    /// False when the claim changed or vanished meanwhile.
    /// </summary>
    Task<bool> TryTakeOver(Guid episodeId, Guid tvShowId, DateTime nowUtc, string holderETag);
}

/// <param name="Claimed">True when the episode is claimed for the requested show.</param>
/// <param name="Holder">The claim of another show when not claimed.</param>
/// <param name="HolderETag">Etag of <paramref name="Holder"/>, for <see cref="ITvShowTrailerClaimStore.TryTakeOver"/>.</param>
public sealed record TvShowTrailerClaimAttempt(bool Claimed, TvShowTrailerClaim? Holder = null, string? HolderETag = null);
