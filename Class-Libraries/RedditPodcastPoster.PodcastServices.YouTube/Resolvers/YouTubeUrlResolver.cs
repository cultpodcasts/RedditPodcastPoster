using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;
using RedditPodcastPoster.PodcastServices.YouTube.Exceptions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public class YouTubeItemResolver(
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    ICachedTolerantYouTubePlaylistService youTubePlaylistService,
    IYouTubeChannelVideosService youTubeChannelVideosService,
    IYouTubeChannelVideoRetrievalPolicy youTubeChannelVideoRetrievalPolicy,
    IYouTubeSearchResultFinder searchResultFinder,
    IPlaylistItemFinder playlistItemFinder,
    ILogger<YouTubeItemResolver> logger)
    : IYouTubeItemResolver
{
    public async Task<FindEpisodeResponse?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext)
    {
        var youTubePublishingDelay = request.Podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay < TimeSpan.Zero)
        {
            indexingContext = new IndexingContext(
                request.Episode.HasAccurateReleaseTime()
                    ? request.Episode.Release.Add(youTubePublishingDelay)
                    : DateTime.UtcNow.Add(youTubePublishingDelay),
                indexingContext.IndexSpotify,
                indexingContext.SkipYouTubeUrlResolving,
                indexingContext.SkipSpotifyUrlResolving,
                indexingContext.SkipExpensiveYouTubeQueries,
                indexingContext.SkipPodcastDiscovery,
                indexingContext.SkipExpensiveSpotifyQueries,
                indexingContext.SkipShortEpisodes);
        }

        if (!string.IsNullOrWhiteSpace(request.Podcast.YouTubePlaylistId))
        {
            return await GetPlaylistVideos(request, indexingContext, youTubePublishingDelay);
        }

        return await GetChannelVideos(request, indexingContext, youTubePublishingDelay);
    }

    private async Task<FindEpisodeResponse?> GetPlaylistVideos(EnrichmentRequest request,
        IndexingContext indexingContext, TimeSpan youTubePublishingDelay)
    {
        var playlistId = new YouTubePlaylistId(request.Podcast.YouTubePlaylistId, YouTubePlaylistIdSource.PodcastEntity, request.Podcast.Id.ToString());
        var latestPlaylistItems = await youTubePlaylistService.GetPlaylistVideoSnippets(
            playlistId,
            indexingContext, true,
            indexingContext.RunExpensiveYouTubePlaylistPagination(request.Podcast),
            request.Podcast.YouTubePlaylistOrder);
        if (latestPlaylistItems.Result == null)
        {
            if (latestPlaylistItems.Failure != null)
            {
                LogPlaylistFetchFailure(request.Podcast, latestPlaylistItems.Failure, playlistId);
            }

            return null;
        }

        if (latestPlaylistItems.Result.Any())
        {
            if (indexingContext.ReleasedSince.HasValue)
            {
                logger.LogInformation(
                    "{method} Retrieved {count} items published on YouTube since '{releasedSince:R}'",
                    nameof(GetPlaylistVideos), latestPlaylistItems.Result.Count, indexingContext.ReleasedSince.Value);
            }
            else
            {
                logger.LogInformation(
                    "{method} Retrieved {count} items published on YouTube. {releasedSince} is Null.",
                    nameof(GetPlaylistVideos), latestPlaylistItems.Result.Count, nameof(indexingContext.ReleasedSince));
            }

            var matchedYouTubeVideo = await playlistItemFinder.FindMatchingYouTubeVideo(
                request.Episode,
                latestPlaylistItems.Result,
                youTubePublishingDelay,
                indexingContext,
                request.Podcast);
            return matchedYouTubeVideo;
        }

        return new FindEpisodeResponse();
    }

    private async Task<FindEpisodeResponse?> GetChannelVideos(
        EnrichmentRequest request, IndexingContext indexingContext, TimeSpan youTubePublishingDelay)
    {
        var uploadsPlaylistReason = youTubeChannelVideoRetrievalPolicy.GetUploadsPlaylistReason(request.Podcast);
        if (uploadsPlaylistReason != null)
        {
            logger.LogInformation(
                "Using channel uploads playlist for channel-id '{ChannelId}' ({Reason}).",
                request.Podcast.YouTubeChannelId, uploadsPlaylistReason);
            return await GetChannelUploadsPlaylistVideos(request, indexingContext, youTubePublishingDelay);
        }

        try
        {
            var searchListResponse =
                await youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(
                    new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexingContext);
            if (searchListResponse == null)
            {
                return null;
            }

            if (searchListResponse.Any())
            {
                LogRetrievedCount(nameof(GetChannelVideos), searchListResponse.Count, indexingContext);
            }

            return await searchResultFinder.FindMatchingYouTubeVideo(
                request.Episode,
                searchListResponse,
                youTubePublishingDelay,
                indexingContext,
                request.Podcast);
        }
        catch (YouTubeChannelSearchForbiddenException ex)
        {
            logger.LogInformation(ex,
                "Search.List is not permitted for channel-id '{ChannelId}'; falling back to channel uploads playlist.",
                request.Podcast.YouTubeChannelId);
            request.Podcast.YouTubeChannelSearchForbidden = true;
            return await GetChannelUploadsPlaylistVideos(request, indexingContext, youTubePublishingDelay);
        }
    }

    private async Task<FindEpisodeResponse?> GetChannelUploadsPlaylistVideos(
        EnrichmentRequest request, IndexingContext indexingContext, TimeSpan youTubePublishingDelay)
    {
        var channelVideosResponse = await youTubeChannelVideosService.GetChannelVideos(
            new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexingContext);
        if (channelVideosResponse.PlaylistItems == null)
        {
            if (channelVideosResponse.Failure != null)
            {
                LogPlaylistFetchFailure(request.Podcast, channelVideosResponse.Failure,
                    new YouTubePlaylistId(
                        channelVideosResponse.Channel?.ContentDetails?.RelatedPlaylists?.Uploads ?? string.Empty,
                        YouTubePlaylistIdSource.ChannelUploads,
                        request.Podcast.YouTubeChannelId));
            }

            return null;
        }

        var playlistItems = channelVideosResponse.PlaylistItems.ForEpisodeMatching(indexingContext);
        if (playlistItems.Any())
        {
            LogRetrievedCount(nameof(GetChannelUploadsPlaylistVideos), playlistItems.Count, indexingContext);
        }

        return await playlistItemFinder.FindMatchingYouTubeVideo(
            request.Episode,
            playlistItems,
            youTubePublishingDelay,
            indexingContext,
            request.Podcast);
    }

    private void LogRetrievedCount(string method, int count, IndexingContext indexingContext)
    {
        if (indexingContext.ReleasedSince.HasValue)
        {
            logger.LogInformation(
                "{method} Retrieved {count} items published on YouTube since '{releasedSince:R}'",
                method, count, indexingContext.ReleasedSince.Value);
        }
        else
        {
            logger.LogInformation(
                "{method} Retrieved {count} items published on YouTube. {releasedSince} is Null.",
                method, count, nameof(indexingContext.ReleasedSince));
        }
    }

    private void LogPlaylistFetchFailure(RedditPodcastPoster.Models.Podcasts.Podcast podcast,
        YouTubePlaylistFetchFailure? failure, YouTubePlaylistId playlistId)
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
}
