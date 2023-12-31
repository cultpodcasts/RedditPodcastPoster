﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeItemResolver : IYouTubeItemResolver
{
    private readonly ILogger _logger;
    private readonly IYouTubeChannelVideoSnippetsService _youTubeChannelVideoSnippetsService;
    private readonly IYouTubeSearcher _youTubeSearcher;

    public YouTubeItemResolver(
        IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
        IYouTubeSearcher youTubeSearcher,
        ILogger<YouTubeItemResolver> logger)
    {
        _youTubeChannelVideoSnippetsService = youTubeChannelVideoSnippetsService;
        _youTubeSearcher = youTubeSearcher;
        _logger = logger;
    }

    public async Task<FindEpisodeResponse?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext)
    {
        var youTubePublishingDelay = request.Podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay < TimeSpan.Zero)
        {
            indexingContext = new IndexingContext(
                DateTime.UtcNow.Add(youTubePublishingDelay),
                indexingContext.SkipYouTubeUrlResolving,
                indexingContext.SkipSpotifyUrlResolving,
                indexingContext.SkipExpensiveYouTubeQueries,
                indexingContext.SkipPodcastDiscovery,
                indexingContext.SkipExpensiveSpotifyQueries,
                indexingContext.SkipShortEpisodes);
        }

        var searchListResponse =
            await _youTubeChannelVideoSnippetsService.GetLatestChannelVideoSnippets(
                new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexingContext);
        if (searchListResponse == null)
        {
            return null;
        }

        if (searchListResponse.Any())
        {
            if (indexingContext.ReleasedSince.HasValue)
            {
                _logger.LogInformation(
                    $"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube since '{indexingContext.ReleasedSince.Value:R}'");
            }
            else
            {
                _logger.LogInformation(
                    $"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube. {nameof(indexingContext.ReleasedSince)} is Null.");
            }
        }

        var matchedYouTubeVideo = await _youTubeSearcher.FindMatchingYouTubeVideo(
            request.Episode,
            searchListResponse,
            youTubePublishingDelay,
            indexingContext);
        return matchedYouTubeVideo;
    }
}