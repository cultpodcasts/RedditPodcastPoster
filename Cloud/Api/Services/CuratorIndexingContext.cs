using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Services;

/// <summary>
/// The indexing context for a curator's on-demand "Index" (podcast, TV show or news organisation): every resolver and
/// expensive query on, Spotify indexed, no podcast discovery, and no YouTube indexing hints (only the hourly indexer
/// may read large playlists from the uploads head; a curator index always walks them in full).
/// </summary>
/// <remarks>
/// Default mode uses <see cref="IndexerOptions.ReleasedDaysAgo"/> (prod: 2). Backlog / cold-start mode
/// (<paramref name="backlog"/>) clears <see cref="IndexingContext.ReleasedSince"/> so provider pagination walks the
/// full catalogue (docs/catalogue-pagination.md). Same on-demand HTTP budget still applies — a long walk may 504
/// TimedOut (cultpodcasts/RedditPodcastPoster#1310).
/// </remarks>
public static class CuratorIndexingContext
{
    public static IndexingContext Create(IndexerOptions indexerOptions, bool backlog = false)
    {
        if (!backlog && indexerOptions.ReleasedDaysAgo == null)
        {
            throw new InvalidOperationException("Unable to index with null released-days-ago.");
        }

        var baseline = backlog
            ? new IndexingContext(ReleasedSince: null, SkipYouTubeUrlResolving: indexerOptions.ByPassYouTube)
            : indexerOptions.ToIndexingContext();

        return baseline with
        {
            IndexSpotify = true,
            SkipSpotifyUrlResolving = false,
            SkipYouTubeUrlResolving = false,
            SkipExpensiveYouTubeQueries = false,
            SkipExpensiveSpotifyQueries = false,
            SkipPodcastDiscovery = true
        };
    }
}
