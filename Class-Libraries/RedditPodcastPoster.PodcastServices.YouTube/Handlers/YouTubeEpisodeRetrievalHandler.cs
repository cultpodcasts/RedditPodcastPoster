using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Handlers;

public class YouTubeEpisodeRetrievalHandler(
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ILogger<YouTubeEpisodeRetrievalHandler> logger)
    : IYouTubeEpisodeRetrievalHandler
{
    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IEnumerable<EpisodeModel> episodes, IndexingContext indexingContext)
    {
        var handled = false;
        IList<EpisodeModel> newEpisodes = new List<EpisodeModel>();
        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            LogDiscoveryPath(podcast, "skipped-no-channel", indexingContext, 0);
            return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
        }

        if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
        {
            var arbitraryOrder = podcast.HasArbitraryYouTubePlaylistOrder();
            var runExpensivePagination = indexingContext.RunExpensiveYouTubePlaylistPagination(podcast);
            // Skip-expensive passes read only the playlist head unless it is newest-first (then they page while
            // items are inside the window); the full walk of an unordered playlist waits for the expensive pass.
            var firstPageOnlyUnlessNewestFirst = !arbitraryOrder && indexingContext.SkipExpensiveYouTubeQueries;
            var discoveryPath = arbitraryOrder
                ? "playlist-arbitrary-full-walk"
                : firstPageOnlyUnlessNewestFirst
                    ? "playlist-head-only"
                    : "playlist-paginated";
            if (!arbitraryOrder && podcast.HasExpensiveYouTubePlaylistQuery() &&
                indexingContext.SkipExpensiveYouTubeQueries)
            {
                logger.LogInformation(
                    "Podcast '{PodcastId}' has known expensive playlist query; using single-page playlist fetch this pass.",
                    podcast.Id);
            }

            var playlistId = new YouTubePlaylistId(podcast.YouTubePlaylistId,
                YouTubePlaylistIdSource.PodcastEntity, podcast.Id.ToString());
            var getPlaylistEpisodesResult = await youTubeEpisodeProvider.GetPlaylistEpisodes(
                podcast,
                playlistId,
                new YouTubeChannelId(podcast.YouTubeChannelId),
                indexingContext, runExpensivePagination, podcast.YouTubePlaylistOrder,
                KnownYouTubeIds(episodes, indexingContext),
                firstPageOnlyUnlessNewestFirst);
            if (getPlaylistEpisodesResult.Results != null)
            {
                newEpisodes = getPlaylistEpisodesResult.Results;
            }

            logger.LogPlaylistFetchFailure(podcast, getPlaylistEpisodesResult.Failure, playlistId);

            // Arbitrary-order playlists never yield a head-order probe result (IsExpensiveQuery stays
            // null), and even if a probe value sneaks through the expensive flag must stay untouched —
            // curated playlists have no positional order to learn from.
            if (!arbitraryOrder && getPlaylistEpisodesResult.IsExpensiveQuery.HasValue)
            {
                YouTubeExpensiveQueryFlag.Apply(
                    podcast,
                    getPlaylistEpisodesResult.IsExpensiveQuery,
                    YouTubeExpensiveQueryFlag.MinimumOrderSampleSize,
                    logger);
            }

            LogDiscoveryPath(podcast, discoveryPath, indexingContext, newEpisodes.Count);
        }
        else
        {
            var foundEpisodes = await youTubeEpisodeProvider.GetEpisodes(
                podcast, indexingContext, KnownYouTubeIds(episodes, indexingContext));
            if (foundEpisodes != null)
            {
                newEpisodes = foundEpisodes;
            }

            LogDiscoveryPath(podcast, "channel", indexingContext, newEpisodes.Count);
        }

        handled = true;

        return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
    }

    /// <summary>
    /// YouTube ids of stored episodes inside the window; discovery skips them on both the channel and playlist paths.
    /// </summary>
    private static IReadOnlyCollection<string> KnownYouTubeIds(
        IEnumerable<EpisodeModel> episodes,
        IndexingContext indexingContext)
    {
        var scoped = indexingContext.ReleasedSince.HasValue
            ? episodes.Where(x => x.ReleaseUtc >= indexingContext.ReleasedSince)
            : episodes;
        return scoped.Select(x => EpisodeServicePresence.YouTubeEpisodeId(x) ?? string.Empty).ToArray();
    }

    private void LogDiscoveryPath(Podcast podcast, string discoveryPath, IndexingContext indexingContext, int episodesFound)
    {
        if (podcast.DependsOnYouTubeForEpisodeDiscovery())
        {
            logger.LogWarning(
                "YouTubeDiscoveryPath podcast-id='{PodcastId}' path='{DiscoveryPath}' youtube-authority='{YouTubeAuthority}' skip-youtube='{SkipYouTube}' skip-expensive-youtube='{SkipExpensiveYouTube}' episodes-found='{EpisodesFound}'",
                podcast.Id, discoveryPath, true,
                indexingContext.SkipYouTubeUrlResolving, indexingContext.SkipExpensiveYouTubeQueries, episodesFound);
        }
        else
        {
            logger.LogInformation(
                "YouTubeDiscoveryPath podcast-id='{PodcastId}' path='{DiscoveryPath}' youtube-authority='{YouTubeAuthority}' skip-youtube='{SkipYouTube}' skip-expensive-youtube='{SkipExpensiveYouTube}' episodes-found='{EpisodesFound}'",
                podcast.Id, discoveryPath, false,
                indexingContext.SkipYouTubeUrlResolving, indexingContext.SkipExpensiveYouTubeQueries, episodesFound);
        }
    }
}
