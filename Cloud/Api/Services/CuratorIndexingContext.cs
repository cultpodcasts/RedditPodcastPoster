using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Services;

/// <summary>
/// The indexing context for a curator's on-demand "Index" (podcast, TV show or news organisation): every resolver and
/// expensive query on, Spotify indexed, no podcast discovery, and no YouTube indexing hints (only the hourly indexer
/// may read large playlists from the uploads head; a curator index always walks them in full).
/// </summary>
/// <remarks>
/// <see cref="Create"/> uses <see cref="IndexerOptions.ReleasedDaysAgo"/> (prod: 2).
/// <see cref="CreateBacklog"/> sets <see cref="IndexingContext.ReleasedSince"/> to
/// <see cref="BacklogReleasedSince"/> (Unix epoch UTC) so <c>PodcastUpdater</c> accepts the context while provider
/// pagination still walks essentially the full catalogue (docs/catalogue-pagination.md). Same on-demand HTTP budget
/// still applies — a long walk may 504 TimedOut (cultpodcasts/RedditPodcastPoster#1310).
/// </remarks>
public static class CuratorIndexingContext
{
    /// <summary>
    /// Cold-start / backlog floor: far enough in the past that newest-first pagination stops only before real
    /// catalogue history, and Cosmos loads episodes on or after this instant. Must be non-null —
    /// <c>PodcastUpdater.Update</c> throws when <see cref="IndexingContext.ReleasedSince"/> is unset.
    /// </summary>
    public static readonly DateTime BacklogReleasedSince = DateTime.UnixEpoch;

    public static IndexingContext Create(IndexerOptions indexerOptions)
    {
        if (indexerOptions.ReleasedDaysAgo == null)
        {
            throw new InvalidOperationException("Unable to index with null released-days-ago.");
        }

        return ApplyCuratorFlags(indexerOptions.ToIndexingContext());
    }

    /// <summary>
    /// Full-catalogue curator walk: <see cref="IndexingContext.ReleasedSince"/> is
    /// <see cref="BacklogReleasedSince"/> (not the hub Index lookback). Does not read
    /// <see cref="IndexerOptions.ReleasedDaysAgo"/>.
    /// </summary>
    public static IndexingContext CreateBacklog() =>
        ApplyCuratorFlags(new IndexingContext(ReleasedSince: BacklogReleasedSince));

    private static IndexingContext ApplyCuratorFlags(IndexingContext baseline) =>
        baseline with
        {
            IndexSpotify = true,
            SkipSpotifyUrlResolving = false,
            SkipYouTubeUrlResolving = false,
            SkipExpensiveYouTubeQueries = false,
            SkipExpensiveSpotifyQueries = false,
            SkipPodcastDiscovery = true
        };
}
