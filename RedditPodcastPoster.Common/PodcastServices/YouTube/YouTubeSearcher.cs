using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeSearcher : IYouTubeSearcher
{
    private static readonly Regex NumberMatch = new(@"(?'number'\d+)", RegexOptions.Compiled);
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
        var candidates = searchResults.Where(searchResult =>
        {
            if (searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime < episode.Release)
            {
                var publishedToYouTubeBefore =
                    episode.Release - searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime;

                if (publishedToYouTubeBefore < TimeSpan.FromHours(4))
                {
                    _logger.LogInformation(
                        $"{nameof(FindMatchingYouTubeVideo)} Including candidate-match '{searchResult.Snippet.Title}' as was published to YouTube within 4-hours before. YouTube release '{searchResult.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime:R}'. Episode release '{episode.Release:R}'.");
                    return true;
                }
            }

            var youTubePublishDelayAfterPodcastRelease =
                searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime - episode.Release;

            var isWithinYouTubePublishWaitingDelay = youTubePublishDelayAfterPodcastRelease < youTubePublishingDelay;
            _logger.LogInformation(
                $"{nameof(FindMatchingYouTubeVideo)} Including candidate-match? {isWithinYouTubePublishWaitingDelay} - '{searchResult.Snippet.Title}' as released before episode. YouTube release '{searchResult.Snippet.PublishedAtDateTimeOffset.Value.UtcDateTime:R}'. Episode release '{episode.Release:R}'.");


            return isWithinYouTubePublishWaitingDelay;
        }).ToList();

        var episodeNumberMatch = NumberMatch.Match(episode.Title);
        if (episodeNumberMatch.Success)
        {
            if (int.TryParse(episodeNumberMatch.Groups["number"].Value, out var episodeNumber))
            {
                var matchingSearchResult = searchResults.Where(x =>
                        NumberMatch.IsMatch(x.Snippet.Title) &&
                        int.TryParse(NumberMatch.Match(x.Snippet.Title).Value, out _))
                    .Where(x =>
                        int.Parse(NumberMatch.Match(x.Snippet.Title).Groups["number"].Value) == episodeNumber);

                if (matchingSearchResult.Count() == 1)
                {
                    return matchingSearchResult.Single();
                }

                if (matchingSearchResult.Any())
                {
                    _logger.LogInformation(
                        $"Could not match on number that appears in title '{episodeNumber}' as appears in multiple episode-titles: {string.Join(", ", matchingSearchResult.Select(x => $"'{x}'"))}.");
                }
            }
        }

        var order = candidates
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