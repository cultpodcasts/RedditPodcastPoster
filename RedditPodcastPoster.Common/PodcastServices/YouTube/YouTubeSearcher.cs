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

    public SearchResult? FindMatchingYouTubeVideo(
        Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan youTubePublishingDelay)
    {
        _logger.LogInformation($"{nameof(FindMatchingYouTubeVideo)} Find matching episode for '{episode.Title}' released at '{episode.Release:R}'.");
        var withinPublishingDelayThreshold = searchResults.Where(searchResult =>
        {
            if (searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime < episode.Release)
            {
                _logger.LogInformation($"{nameof(FindMatchingYouTubeVideo)} Including candidate-match '{searchResult.Snippet.Title}' as released before episode. YouTube release '{searchResult.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime:R}'. Episode release '{episode.Release:R}'.");
                return true;
            }

            var youTubePublishDelayAfterPodcastRelease =
                searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime - episode.Release;

            var isWithinYouTubePublishWaitingDelay = youTubePublishDelayAfterPodcastRelease < youTubePublishingDelay;
            _logger.LogInformation($"{nameof(FindMatchingYouTubeVideo)} Including candidate-match? {isWithinYouTubePublishWaitingDelay} - '{searchResult.Snippet.Title}' as released before episode. YouTube release '{searchResult.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime:R}'. Episode release '{episode.Release:R}'.");


            return isWithinYouTubePublishWaitingDelay;
        }).ToList();

        var order = withinPublishingDelayThreshold
            .OrderByDescending(x => Levenshtein.CalculateSimilarity(episode.Title, x.Snippet.Title))
            .ToList();

        var matchedYouTubeVideo = order.FirstOrDefault();
        if (matchedYouTubeVideo != null)
        {
            _logger.LogInformation(
                $"{nameof(FindMatchingYouTubeVideo)} Matched You-Tube search-result '{matchedYouTubeVideo.Snippet.Title}' released '{matchedYouTubeVideo.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime:R}' with episode '{episode.Title}' released '{episode.Release:R}'.");
        }
        else
        {
            _logger.LogInformation(
                $"{nameof(FindMatchingYouTubeVideo)} Did not match with a You-Tube search-result for episode '{episode.Title}' released '{episode.Release:R}'.");
        }

        return matchedYouTubeVideo;
    }
}