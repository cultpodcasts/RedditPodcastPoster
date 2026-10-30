using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Episodes;

/// <summary>
/// Picks the latest episode by <see cref="Episode.ReleaseUtc"/> after deserialize.
/// The podcast partition is scanned and folded in process (introduced when Cosmos could not
/// ORDER BY the old release dual-key, error 2206; kept so the result does not depend on the
/// container's range index for <c>releaseSort</c>).
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
