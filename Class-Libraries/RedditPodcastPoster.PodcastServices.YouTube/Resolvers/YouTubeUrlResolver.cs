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
        var latestPlaylistItems = await youTubePlaylistService.GetPlaylistVideoSnippets(
            new YouTubePlaylistId(request.Podcast.YouTubePlaylistId), indexingContext, true,
            indexingContext.RunExpensiveYouTubePlaylistPagination(request.Podcast));
        if (latestPlaylistItems?.Result == null)
        {
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
                indexingContext);
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
                indexingContext);
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
        var channelVideos = await youTubeChannelVideosService.GetChannelVideos(
            new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexingContext);
        if (channelVideos?.PlaylistItems == null)
        {
            return null;
        }

        var playlistItems = channelVideos.PlaylistItems.ForEpisodeMatching(indexingContext);
        if (playlistItems.Any())
        {
            LogRetrievedCount(nameof(GetChannelUploadsPlaylistVideos), playlistItems.Count, indexingContext);
        }

        return await playlistItemFinder.FindMatchingYouTubeVideo(
            request.Episode,
            playlistItems,
            youTubePublishingDelay,
            indexingContext);
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
}
