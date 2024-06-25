using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public partial class SearchResultFinder(
    IYouTubeVideoService videoService,
    ILogger<SearchResultFinder> logger)
    : ISearchResultFinder
{
    private const int MinFuzzyScore = 70;
    private static readonly Regex NumberMatch = CreateNumberMatch();
    private static readonly TimeSpan VideoDurationTolerance = TimeSpan.FromMinutes(2);

    public async Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(
        Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext)
    {
        var match = MatchOnExactTitle(episode, searchResults);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        match = MatchOnEpisodeNumber(episode, searchResults);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        var videoMatch = await MatchOnEpisodeDuration(episode, searchResults, indexingContext);
        if (videoMatch != null)
        {
            return videoMatch;
        }

        if (episode.HasAccurateReleaseTime() && youTubePublishDelay.HasValue)
        {
            match = MatchOnPublishTimeComparedToPublishDelay(episode, searchResults, youTubePublishDelay.Value);
            if (match != null && FuzzyMatcher.IsMatch(episode.Title, match, s => s.Snippet.Title, MinFuzzyScore))
            {
                return new FindEpisodeResponse(match);
            }
        }

        match = MatchOnTextCloseness(episode, searchResults);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        return null;
    }

    private async Task<FindEpisodeResponse?> MatchOnEpisodeDuration(
        Episode episode,
        IList<SearchResult> searchResults,
        IndexingContext indexingContext)
    {
        var videoDetails =
            await videoService.GetVideoContentDetails(searchResults.Select(x => x.Id.VideoId).ToList(),
                indexingContext);
        if (videoDetails != null && videoDetails.Any())
        {
            var matchingVideo =
                videoDetails.MinBy(x => Math.Abs((episode.Length - x.GetLength() ?? TimeSpan.Zero).Ticks));
            var searchResult = searchResults.FirstOrDefault(x => x.Id.VideoId == matchingVideo!.Id);
            if (searchResult != null)
            {
                var matchingPair = new FindEpisodeResponse(searchResult, matchingVideo);
                if (Math.Abs((matchingPair.Video!.GetLength() ?? TimeSpan.Zero - episode.Length).Ticks) <
                    VideoDurationTolerance.Ticks)
                {
                    logger.LogInformation(
                        $"Matched episode '{episode.Title}' and length: '{episode.Length:g}' with episode '{matchingPair.SearchResult.Snippet.Title}' having length: '{matchingPair.Video?.GetLength():g}'.");
                    return matchingPair;
                }
            }
        }

        return null;
    }

    private SearchResult? MatchOnTextCloseness(Episode episode, IList<SearchResult> searchResults)
    {
        return FuzzyMatcher.Match(episode.Title, searchResults, x => x.Snippet.Title, MinFuzzyScore);
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
                    logger.LogInformation($"Matched on episode-number '{episodeNumber}'.");
                    return matchingSearchResult.Single();
                }

                if (matchingSearchResult.Any())
                {
                    logger.LogInformation(
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
        var closestEpisode = searchResults
            .Where(x => x.Snippet.PublishedAtDateTimeOffset.HasValue)
            .MinBy(x => Math.Abs(x.Snippet.PublishedAtDateTimeOffset!.Value.Subtract(expectedPublish).Ticks));
        if (closestEpisode?.Snippet.PublishedAtDateTimeOffset != null &&
            closestEpisode.Snippet.PublishedAtDateTimeOffset.Value.Subtract(expectedPublish) < TimeSpan.FromDays(1))
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
            logger.LogInformation($"Matched on episode-number '{episode.Title}'.");
            return matchingSearchResult.Single();
        }

        if (matchingSearchResult.Any())
        {
            logger.LogInformation(
                $"Matched multiple items on episode-title '{episode.Title}'.");
        }

        return null;
    }

    [GeneratedRegex("(?'number'\\d+)", RegexOptions.Compiled)]
    private static partial Regex CreateNumberMatch();
}