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
}
