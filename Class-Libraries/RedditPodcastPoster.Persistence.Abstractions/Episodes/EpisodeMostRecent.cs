using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Picks the latest episode by <see cref="Episode.ReleaseUtc"/> after deserialize.
/// Cosmos cannot ORDER BY the dual-key ternary
/// <c>IS_DEFINED(releaseSort) ? releaseSort : release</c> (error 2206: expression is not a
/// document path). Scan the podcast partition and use this in process instead.
/// </summary>
public static class EpisodeMostRecent
{
    public static Episode? Of(IEnumerable<Episode> episodes) =>
        episodes.MaxBy(episode => episode.ReleaseUtc);
}
