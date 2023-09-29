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

    public async Task<SearchResult?> FindEpisode(Podcast podcast, Episode episode, DateTime? publishedSince)
    {
        var youTubePublishingDelay = TimeSpan.Parse(podcast.YouTubePublishingDelayTimeSpan);
        var searchListResponse =
            await _youTubeSearchService.GetLatestChannelVideos(
                new GetLatestYouTubeChannelVideosRequest(podcast.YouTubeChannelId, publishedSince));
        if (publishedSince.HasValue)
        {
            _logger.LogInformation($"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube since '{publishedSince.Value:R}'");
        }
        else
        {
            _logger.LogInformation($"{nameof(FindEpisode)} Retrieved {searchListResponse.Count} items published on YouTube. {nameof(publishedSince)} is Null.");

        }
        var matchedYouTubeVideo =
            _youTubeSearcher.FindMatchingYouTubeVideo(episode, searchListResponse, youTubePublishingDelay);
        return matchedYouTubeVideo;
    }
}