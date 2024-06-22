using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeItemResolver(
    IYouTubeChannelVideoSnippetsService youTubeChannelVideoSnippetsService,
    ISearchResultFinder searchResultFinder,
    ILogger<YouTubeItemResolver> logger)
    : IYouTubeItemResolver
{
    public async Task<FindEpisodeResponse?> FindEpisode(EnrichmentRequest request, IndexingContext indexingContext)
    {
        var youTubePublishingDelay = request.Podcast.YouTubePublishingDelay();
        if (youTubePublishingDelay < TimeSpan.Zero)
        {
            indexingContext = new IndexingContext(
                request.Episode.HasAccurateReleaseTime()?request.Episode.Release.Add(youTubePublishingDelay):
                    DateTime.UtcNow.Add(youTubePublishingDelay),
                SkipYouTubeUrlResolving: indexingContext.SkipYouTubeUrlResolving,
                SkipSpotifyUrlResolving: indexingContext.SkipSpotifyUrlResolving,
                SkipExpensiveYouTubeQueries: indexingContext.SkipExpensiveYouTubeQueries,
                SkipPodcastDiscovery: indexingContext.SkipPodcastDiscovery,
                SkipExpensiveSpotifyQueries: indexingContext.SkipExpensiveSpotifyQueries,
                SkipShortEpisodes: indexingContext.SkipShortEpisodes);
        }

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
                    $"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube since '{indexingContext.ReleasedSince.Value:R}'");
            }
            else
            {
                logger.LogInformation(
                    $"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube. {nameof(indexingContext.ReleasedSince)} is Null.");
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