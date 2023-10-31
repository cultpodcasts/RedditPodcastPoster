using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public partial class YouTubeSearcher : IYouTubeSearcher
{
    private static readonly Regex NumberMatch = CreateNumberMatch();
    private readonly ILogger<YouTubeSearcher> _logger;

    public YouTubeSearcher(ILogger<YouTubeSearcher> logger)
    {
        _logger = logger;
    }

    public SearchResult? FindMatchingYouTubeVideo(
        Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay)
    {
        var match = MatchOnExactTitle(episode, searchResults);
        if (match != null)
        {
            return match;
        }

        match = MatchOnEpisodeNumber(episode, searchResults);
        if (match != null)
        {
            return match;
        }

        if (episode.HasAccurateReleaseTime() && youTubePublishDelay.HasValue)
        {
            match = MatchOnPublishTimeComparedToPublishDelay(episode, searchResults, youTubePublishDelay.Value);
            if (match != null)
            {
                return match;
            }
        }

        match = MatchOnTextCloseness(episode, searchResults);
        return match;
    }

    private SearchResult? MatchOnTextCloseness(Episode episode, IList<SearchResult> searchResults)
    {
        return searchResults.MaxBy(x => Levenshtein.CalculateSimilarity(episode.Title, x.Snippet.Title));
    }

    private SearchResult? MatchOnEpisodeNumber(Episode episode, IList<SearchResult> searchResults)
    {
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
                    _logger.LogInformation($"Matched on episode-number '{episodeNumber}'.");
                    return matchingSearchResult.Single();
                }

                if (matchingSearchResult.Any())
                {
                    _logger.LogInformation(
                        $"Could not match on number that appears in title '{episodeNumber}' as appears in multiple episode-titles: {string.Join(", ", matchingSearchResult.Select(x => $"'{x}'"))}.");
                }
            }
        }

        return null;
    }

    private SearchResult? MatchOnPublishTimeComparedToPublishDelay(
        Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan youTubePublishDelay)
    {
        var expectedPublish = episode.Release.Add(youTubePublishDelay);
        var closestEpisode = searchResults.MinBy(x =>
            Math.Abs(x.Snippet.PublishedAtDateTimeOffset.Value.Subtract(expectedPublish).Ticks));
        if (closestEpisode.Snippet.PublishedAtDateTimeOffset.Value.Subtract(expectedPublish) < TimeSpan.FromDays(1))
        {
            return closestEpisode;
        }

        return null;
    }

    private SearchResult? MatchOnExactTitle(Episode episode, IList<SearchResult> searchResults)
    {
        var episodeTitle = episode.Title.Trim().ToLower();
        var matchingSearchResult = searchResults.Where(x =>
            x.Snippet.Title.Trim().ToLower() == episodeTitle);

        if (matchingSearchResult.Count() == 1)
        {
            _logger.LogInformation($"Matched on episode-number '{episode.Title}'.");
            return matchingSearchResult.Single();
        }

        if (matchingSearchResult.Any())
        {
            _logger.LogInformation(
                $"Matched multiple items on episode-title '{episode.Title}'.");
        }

        return null;
    }

    [GeneratedRegex("(?'number'\\d+)", RegexOptions.Compiled)]
    private static partial Regex CreateNumberMatch();
}