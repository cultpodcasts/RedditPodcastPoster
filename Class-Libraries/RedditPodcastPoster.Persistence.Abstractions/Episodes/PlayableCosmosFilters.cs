using System.Linq.Expressions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Cosmos LINQ predicates for any playable container (TvShowEpisode, NewsReport, Film).
/// Same release rule as <see cref="EpisodeCosmosFilters.ReleasedOnOrAfter"/>.
/// </summary>
public static class PlayableCosmosFilters
{
    public static Expression<Func<TPlayable, bool>> ReleasedOnOrAfter<TPlayable>(DateTime since)
        where TPlayable : Playable =>
        e => e.ReleaseSort >= since;

    /// <summary>
    /// Playables released on or after <paramref name="since"/> that already hold a Spotify or Apple link. Read by the
    /// catalogue parent Spotify / Apple catch-up so a link owned by a stored item outside the discovery window is never
    /// assigned to a second item (same role as the podcast updater's id holders query).
    /// </summary>
    public static Expression<Func<TPlayable, bool>> HoldsSpotifyOrAppleLinkReleasedOnOrAfter<TPlayable>(DateTime since)
        where TPlayable : Playable =>
        e => e.Services != null &&
             (e.Services[ServiceKeys.Spotify].Url != null || e.Services[ServiceKeys.Apple].Url != null) &&
             e.ReleaseSort >= since;

    /// <summary>
    /// Playables released in <c>[sinceInclusive, beforeExclusive)</c> that still lack a Spotify and/or Apple link the
    /// catch-up is looking up. Used for the older same-title gap read so the pass does not pull every playable in the
    /// matcher's 14-day acceptance (including in-window items and link holders) just to feed
    /// <c>DeferredBehindLongerSameTitle</c>.
    /// </summary>
    /// <remarks>
    /// Missing-service checks use <c>!(Services != null &amp;&amp; Url != null)</c> so a missing dictionary key is treated
    /// the same way as Cosmos (undefined fails <c>!= null</c>), matching
    /// <see cref="HoldsSpotifyOrAppleLinkReleasedOnOrAfter{TPlayable}"/>.
    /// </remarks>
    public static Expression<Func<TPlayable, bool>> MissingAudioLinkReleasedInHalfOpenRange<TPlayable>(
        DateTime sinceInclusive,
        DateTime beforeExclusive,
        bool lookUpSpotify,
        bool lookUpApple)
        where TPlayable : Playable
    {
        if (lookUpSpotify && lookUpApple)
        {
            return e => e.ReleaseSort >= sinceInclusive &&
                        e.ReleaseSort < beforeExclusive &&
                        (!(e.Services != null && e.Services[ServiceKeys.Spotify].Url != null) ||
                         !(e.Services != null && e.Services[ServiceKeys.Apple].Url != null));
        }

        if (lookUpSpotify)
        {
            return e => e.ReleaseSort >= sinceInclusive &&
                        e.ReleaseSort < beforeExclusive &&
                        !(e.Services != null && e.Services[ServiceKeys.Spotify].Url != null);
        }

        return e => e.ReleaseSort >= sinceInclusive &&
                    e.ReleaseSort < beforeExclusive &&
                    !(e.Services != null && e.Services[ServiceKeys.Apple].Url != null);
    }
}
