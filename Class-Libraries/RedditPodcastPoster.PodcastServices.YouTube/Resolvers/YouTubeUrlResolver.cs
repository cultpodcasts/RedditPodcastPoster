using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;
using RedditPodcastPoster.PodcastServices.YouTube.Services;

namespace RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

public class YouTubeItemResolver(
    ICachedTolerantYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    ICachedTolerantYouTubePlaylistService youTubePlaylistService,
    ISearchResultFinder searchResultFinder,
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
        else
        {
            return await GetChannelVideos(request, indexingContext, youTubePublishingDelay);
        }


    }

    private async Task<FindEpisodeResponse?> GetPlaylistVideos(EnrichmentRequest request, IndexingContext indexingContext, TimeSpan youTubePublishingDelay)
    {
        var latestPlaylistItems = await youTubePlaylistService.GetPlaylistVideoSnippets(
            new YouTubePlaylistId(request.Podcast.YouTubePlaylistId),
            indexingContext);
        if (latestPlaylistItems?.Result == null)
            return null;
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
        var searchListResponse =
            await youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(
                new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexingContext);
        if (searchListResponse == null)
        {
            return null;
        }

        if (searchListResponse.Any())
        {
            if (indexingContext.ReleasedSince.HasValue)
            {
                logger.LogInformation(
                    "{method} Retrieved {count} items published on YouTube since '{releasedSince:R}'",
                    nameof(GetChannelVideos), searchListResponse.Count, indexingContext.ReleasedSince.Value);
            }
            else
            {
                logger.LogInformation(
                    "{method} Retrieved {count} items published on YouTube. {releasedSince} is Null.",
                    nameof(GetChannelVideos), searchListResponse.Count, nameof(indexingContext.ReleasedSince));
            }
        }

        var matchedYouTubeVideo = await searchResultFinder.FindMatchingYouTubeVideo(
            request.Episode,
            searchListResponse,
            youTubePublishingDelay,
            indexingContext);
        return matchedYouTubeVideo;
    }
}