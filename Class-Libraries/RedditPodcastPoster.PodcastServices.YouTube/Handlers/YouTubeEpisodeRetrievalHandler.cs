using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
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
            var discoveryPath = arbitraryOrder
                ? "playlist-arbitrary-full-walk"
                : runExpensivePagination
                    ? "playlist-paginated"
                    : "playlist-single-page";
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
                indexingContext, runExpensivePagination, podcast.YouTubePlaylistOrder);
            if (getPlaylistEpisodesResult.Results != null)
            {
                newEpisodes = getPlaylistEpisodesResult.Results;
            }

            LogPlaylistFetchFailure(podcast, getPlaylistEpisodesResult.Failure, playlistId);

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
            IEnumerable<string> knownIds;
            if (indexingContext.ReleasedSince.HasValue)
            {
                knownIds = episodes.Where(x => x.Release >= indexingContext.ReleasedSince)
                    .Select(x => EpisodeServicePresence.YouTubeEpisodeId(x) ?? string.Empty);
            }
            else
            {
                knownIds = episodes.Select(x => EpisodeServicePresence.YouTubeEpisodeId(x) ?? string.Empty);
            }

            var foundEpisodes = await youTubeEpisodeProvider.GetEpisodes(
                podcast, indexingContext, knownIds);
            if (foundEpisodes != null)
            {
                newEpisodes = foundEpisodes;
            }

            LogDiscoveryPath(podcast, "channel", indexingContext, newEpisodes.Count);
        }

        handled = true;

        return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
    }

    private void LogPlaylistFetchFailure(Podcast podcast, YouTubePlaylistFetchFailure? failure,
        YouTubePlaylistId playlistId)
    {
        if (failure == null)
        {
            return;
        }

        if (failure == YouTubePlaylistFetchFailure.NotFound)
        {
            logger.LogError(
                "YouTube playlist '{PlaylistId}' (source: {Source}, identifier: {SourceIdentifier}) for podcast '{PodcastName}' (id '{PodcastId}') was not found. The playlist may have been deleted or made private — find and set a new YouTubePlaylistId.",
                playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier, podcast.Name, podcast.Id);
            return;
        }

        logger.LogError(
            "YouTube playlist fetch failed for podcast '{PodcastName}' (id '{PodcastId}') playlist '{PlaylistId}' (source: {Source}, identifier: {SourceIdentifier}, failure '{Failure}').",
            podcast.Name, podcast.Id, playlistId.PlaylistId, playlistId.Source, playlistId.SourceIdentifier, failure);
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
