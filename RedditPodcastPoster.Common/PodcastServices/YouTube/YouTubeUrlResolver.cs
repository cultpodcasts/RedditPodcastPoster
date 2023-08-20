using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeUrlResolver : IYouTubeUrlResolver
{
    private readonly ILogger _logger;
    private readonly IYouTubeSearcher _youTubeSearcher;
    private readonly IYouTubeSearchService _youTubeSearchService;

    public YouTubeUrlResolver(
        IYouTubeSearchService youTubeSearchService,
        IYouTubeSearcher youTubeSearcher,
        ILogger<YouTubeUrlResolver> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _youTubeSearcher = youTubeSearcher;
        _logger = logger;
    }

    public async Task<Uri?> Resolve(Podcast podcast, Episode episode, DateTime? publishedSince)
    {
        var youTubePublishingDelay = TimeSpan.Parse(podcast.YouTubePublishingDelayTimeSpan).Ticks;
        var searchListResponse = await _youTubeSearchService.GetLatestChannelVideos(podcast, publishedSince);
        var matchedYouTubeVideo =
            _youTubeSearcher.FindMatchingYouTubeVideo(episode, searchListResponse.Items, youTubePublishingDelay);
        if (matchedYouTubeVideo == null)
        {
            return null;
        }

        return GetYouTubeUrl(matchedYouTubeVideo);
    }

    public Uri GetYouTubeUrl(SearchResult matchedYouTubeVideo)
    {
        return new Uri($"https://www.youtube.com/watch?v={matchedYouTubeVideo.Id.VideoId}");
    }
}