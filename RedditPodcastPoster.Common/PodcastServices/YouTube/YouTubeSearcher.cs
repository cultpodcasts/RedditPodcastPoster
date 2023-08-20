using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeSearcher : IYouTubeSearcher
{
    private readonly ILogger<YouTubeSearcher> _logger;

    public YouTubeSearcher(ILogger<YouTubeSearcher> logger)
    {
        _logger = logger;
    }

    public SearchResult? FindMatchingYouTubeVideo(Episode episode, IList<SearchResult> searchResults,
        long youTubePublishingDelay)
    {
        var withinPublishingDelayThreshold = searchResults.Where(x =>
        {
            var difference = x.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime - episode.Release;
            var valueTicks = difference.Ticks;
            return Math.Abs(valueTicks) < youTubePublishingDelay;
        }).ToList();
        var order = withinPublishingDelayThreshold
            .OrderByDescending(x => Levenshtein.CalculateSimilarity(episode.Title, x.Snippet.Title)).ToList();

        var matchedYouTubeVideo = order.FirstOrDefault();
        return matchedYouTubeVideo;
    }
}