using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Picks the latest episode by <see cref="Episode.ReleaseUtc"/> after deserialize.
/// Cosmos cannot ORDER BY the dual-key ternary
/// <c>IS_DEFINED(releaseSort) ? releaseSort : release</c> (error 2206: expression is not a
/// document path). Scan the podcast partition and use this in process instead.
/// Prefer <see cref="OfAsync"/> when the source is an <see cref="IAsyncEnumerable{T}"/> so the
/// partition is folded while streaming instead of buffered.
/// </summary>
public static class EpisodeMostRecent
{
    public static Episode? Of(IEnumerable<Episode> episodes) =>
        episodes.MaxBy(episode => episode.ReleaseUtc);

    public static async Task<Episode?> OfAsync(IAsyncEnumerable<Episode> episodes)
    {
        Episode? mostRecent = null;
        await foreach (var episode in episodes)
        {
            if (mostRecent is null || episode.ReleaseUtc > mostRecent.ReleaseUtc)
            {
                mostRecent = episode;
            }
        }

        return mostRecent;
    }
}
