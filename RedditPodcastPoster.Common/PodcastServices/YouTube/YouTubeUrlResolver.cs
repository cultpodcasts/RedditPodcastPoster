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
        var youTubePublishingDelay = TimeSpan.Parse(podcast.YouTubePublishingDelayTimeSpan).Ticks;
        var searchListResponse = await _youTubeSearchService.GetLatestChannelVideos(podcast, publishedSince);
        var matchedYouTubeVideo =
            _youTubeSearcher.FindMatchingYouTubeVideo(episode, searchListResponse.Items, youTubePublishingDelay);
        return matchedYouTubeVideo;
    }
}