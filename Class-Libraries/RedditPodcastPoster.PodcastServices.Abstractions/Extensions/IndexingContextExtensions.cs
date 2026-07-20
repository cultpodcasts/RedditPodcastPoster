using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class IndexingContextExtensions
{
    /// <summary>
    /// Snapshot for batch-level bypass rollup after each podcast. YouTube bypass is per-podcast
    /// during the batch; Spotify bypass is batch-global once rate-limited.
    /// </summary>
    public record PodcastBatchBypassState(
        bool InitialSkipYouTube,
        bool InitialSkipSpotify,
        bool AnyYouTubeBypassed = false,
        bool AnyYouTubeQuotaExhausted = false,
        bool AnySpotifyBypassed = false);

    /// <summary>
    /// Isolated copy for one podcast in a batch. Always clone from the batch parent (not a prior
    /// podcast clone) so per-podcast YouTube bypass does not leak between iterations, while
    /// batch-global flags such as <see cref="IndexingContext.SkipSpotifyUrlResolving"/> are
    /// inherited from the parent.
    /// </summary>
    public static IndexingContext ForPodcastUpdate(this IndexingContext batchContext) =>
        batchContext with { };

    /// <summary>
    /// Merges bypass flags after a podcast update. Spotify bypass propagates immediately to the
    /// batch parent so subsequent <see cref="ForPodcastUpdate"/> calls skip Spotify; YouTube bypass
    /// is tracked for end-of-batch rollup only.
    /// </summary>
    public static PodcastBatchBypassState AbsorbPodcastPass(
        this IndexingContext batchContext,
        IndexingContext podcastContext,
        PodcastBatchBypassState state)
    {
        if (podcastContext.SkipSpotifyUrlResolving)
        {
            batchContext.SkipSpotifyUrlResolving = true;
        }

        return state with
        {
            AnyYouTubeBypassed = state.AnyYouTubeBypassed ||
                                 (!state.InitialSkipYouTube && podcastContext.SkipYouTubeUrlResolving),
            AnyYouTubeQuotaExhausted = state.AnyYouTubeQuotaExhausted ||
                                       (!state.InitialSkipYouTube && podcastContext.YouTubeQuotaExhausted),
            AnySpotifyBypassed = state.AnySpotifyBypassed ||
                                 (!state.InitialSkipSpotify && podcastContext.SkipSpotifyUrlResolving)
        };
    }

    /// <summary>
    /// Applies tracked YouTube bypass flags to the batch context for orchestration reporting.
    /// Spotify bypass is already on the batch parent via <see cref="AbsorbPodcastPass"/>.
    /// </summary>
    public static void ApplyBatchBypassRollup(this IndexingContext batchContext, PodcastBatchBypassState state)
    {
        if (state.AnyYouTubeBypassed)
        {
            batchContext.SkipYouTubeUrlResolving = true;
        }

        if (state.AnyYouTubeQuotaExhausted)
        {
            batchContext.YouTubeQuotaExhausted = true;
        }
    }

    /// <summary>
    /// Full playlist pagination is only allowed when the podcast is known-expensive and this pass
    /// permits expensive YouTube queries (hour 0 UTC primary pass). Channel listing and single-page
    /// playlist fetches are unaffected by <see cref="IndexingContext.SkipExpensiveYouTubeQueries"/>.
    /// </summary>
    public static bool RunExpensiveYouTubePlaylistPagination(this IndexingContext indexingContext, Podcast podcast)
    {
        return podcast.HasExpensiveYouTubePlaylistQuery() && !indexingContext.SkipExpensiveYouTubeQueries;
    }

    public static List<string> GetIndexingContextChanges(this IndexingContext before, IndexingContext after)
    {
        var changes = new List<string>();

        if (before.ReleasedSince != after.ReleasedSince)
        {
            changes.Add($"{nameof(IndexingContext.ReleasedSince)}: '{before.ReleasedSince:O}' -> '{after.ReleasedSince:O}'");
        }

        if (before.IndexSpotify != after.IndexSpotify)
        {
            changes.Add($"{nameof(IndexingContext.IndexSpotify)}: '{before.IndexSpotify}' -> '{after.IndexSpotify}'");
        }

        if (before.SkipYouTubeUrlResolving != after.SkipYouTubeUrlResolving)
        {
            changes.Add($"{nameof(IndexingContext.SkipYouTubeUrlResolving)}: '{before.SkipYouTubeUrlResolving}' -> '{after.SkipYouTubeUrlResolving}'");
        }

        if (before.YouTubeQuotaExhausted != after.YouTubeQuotaExhausted)
        {
            changes.Add($"{nameof(IndexingContext.YouTubeQuotaExhausted)}: '{before.YouTubeQuotaExhausted}' -> '{after.YouTubeQuotaExhausted}'");
        }

        if (before.SkipSpotifyUrlResolving != after.SkipSpotifyUrlResolving)
        {
            changes.Add($"{nameof(IndexingContext.SkipSpotifyUrlResolving)}: '{before.SkipSpotifyUrlResolving}' -> '{after.SkipSpotifyUrlResolving}'");
        }

        if (before.SkipExpensiveYouTubeQueries != after.SkipExpensiveYouTubeQueries)
        {
            changes.Add($"{nameof(IndexingContext.SkipExpensiveYouTubeQueries)}: '{before.SkipExpensiveYouTubeQueries}' -> '{after.SkipExpensiveYouTubeQueries}'");
        }

        if (before.SkipPodcastDiscovery != after.SkipPodcastDiscovery)
        {
            changes.Add($"{nameof(IndexingContext.SkipPodcastDiscovery)}: '{before.SkipPodcastDiscovery}' -> '{after.SkipPodcastDiscovery}'");
        }

        if (before.SkipExpensiveSpotifyQueries != after.SkipExpensiveSpotifyQueries)
        {
            changes.Add($"{nameof(IndexingContext.SkipExpensiveSpotifyQueries)}: '{before.SkipExpensiveSpotifyQueries}' -> '{after.SkipExpensiveSpotifyQueries}'");
        }

        if (before.SkipShortEpisodes != after.SkipShortEpisodes)
        {
            changes.Add($"{nameof(IndexingContext.SkipShortEpisodes)}: '{before.SkipShortEpisodes}' -> '{after.SkipShortEpisodes}'");
        }

        return changes;
    }

    public static void MarkYouTubeQuotaExhausted(this IndexingContext indexingContext)
    {
        indexingContext.SkipYouTubeUrlResolving = true;
        indexingContext.YouTubeQuotaExhausted = true;
    }
}
