using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace Api.Services;

/// <summary>
/// The indexing context for a curator's on-demand "Index" (podcast, TV show or news organisation): every resolver and
/// expensive query on, Spotify indexed, no podcast discovery, and no YouTube indexing hints (only the hourly indexer
/// may read large playlists from the uploads head; a curator index always walks them in full).
/// </summary>
public static class CuratorIndexingContext
{
    public static IndexingContext Create(IndexerOptions indexerOptions)
    {
        if (indexerOptions.ReleasedDaysAgo == null)
        {
            throw new InvalidOperationException("Unable to index with null released-days-ago.");
        }

        return indexerOptions.ToIndexingContext() with
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
