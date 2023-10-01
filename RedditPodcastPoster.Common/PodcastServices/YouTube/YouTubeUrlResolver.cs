using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeItemResolver : IYouTubeItemResolver
{
    private readonly ILogger _logger;
    private readonly IYouTubeSearcher _youTubeSearcher;
    private readonly IYouTubeSearchService _youTubeSearchService;

    public YouTubeItemResolver(
        IYouTubeSearchService youTubeSearchService,
        IYouTubeSearcher youTubeSearcher,
        ILogger<YouTubeItemResolver> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _youTubeSearcher = youTubeSearcher;
        _logger = logger;
    }

    public async Task<SearchResult?> FindEpisode(EnrichmentRequest request, IndexOptions indexOptions)
    {
        var youTubePublishingDelay = TimeSpan.Parse(request.Podcast.YouTubePublishingDelayTimeSpan);
        var searchListResponse =
            await _youTubeSearchService.GetLatestChannelVideos(
                new YouTubeChannelId(request.Podcast.YouTubeChannelId), indexOptions);
        if (request.ReleasedSince.HasValue)
        {
            _logger.LogInformation($"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube since '{request.ReleasedSince.Value:R}'");
        }
        else
        {
            _logger.LogInformation($"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube. {nameof(request.ReleasedSince)} is Null.");

        }
        var matchedYouTubeVideo =
            _youTubeSearcher.FindMatchingYouTubeVideo(request.Episode, searchListResponse, youTubePublishingDelay);
        return matchedYouTubeVideo;
    }
}