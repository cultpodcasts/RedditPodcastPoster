using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public static class IndexingContextExtensions
{
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
}
