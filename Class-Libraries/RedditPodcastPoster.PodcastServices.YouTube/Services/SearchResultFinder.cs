﻿using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public partial class SearchResultFinder(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService videoService,
    ILogger<SearchResultFinder> logger)
    : ISearchResultFinder
{
    private const int MinFuzzyScore = 70;
    private static readonly Regex NumberMatch = CreateNumberMatch();
    private static readonly TimeSpan VideoDurationTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinDurationForPublicationDate = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VideoDurationToleranceForPublicationDate = TimeSpan.FromMinutes(5);

    public async Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext)
    {
        var match = MatchOnExactTitle(episode, searchResults);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        match = await MatchOnEpisodeNumberAndDuration(episode, searchResults, indexingContext);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        var videoDetails =
            await videoService.GetVideoContentDetails(youTubeService, searchResults.Select(x => x.Id.VideoId).ToList(),
                indexingContext);


        var videoMatch = MatchOnEpisodeDuration(episode, searchResults, videoDetails, indexingContext);
        if (videoMatch != null)
        {
            return videoMatch;
        }

        if (episode.HasAccurateReleaseTime() && youTubePublishDelay.HasValue)
        {
            match = MatchOnPublishTimeComparedToPublishDelay(episode, searchResults, youTubePublishDelay.Value);
            if (match != null && FuzzyMatcher.IsMatch(episode.Title, match, s => s.Snippet.Title, MinFuzzyScore))
            {
                if (videoDetails != null)
                {
                    // verify duration is similar
                    var videoDetail = videoDetails.SingleOrDefault(x => x.Id == match.Id.VideoId);
                    if (videoDetail != null)
                    {
                        var matchingVideoLength = videoDetail.GetLength();
                        if (matchingVideoLength.HasValue)
                        {
                            var matchingVideoLengthDifferentTicks =
                                Math.Abs((matchingVideoLength.Value - episode.Length).Ticks);
                            if (matchingVideoLength > MinDurationForPublicationDate &&
                                matchingVideoLengthDifferentTicks < VideoDurationToleranceForPublicationDate.Ticks)
                            {
                                return new FindEpisodeResponse(match);
                            }
                        }
                    }
                    else
                    {
                        return new FindEpisodeResponse(match);
                    }
                }
            }
        }

        match = MatchOnTextCloseness(episode, searchResults);
        if (match != null)
        {
            return new FindEpisodeResponse(match);
        }

        return null;
    }

    private FindEpisodeResponse? MatchOnEpisodeDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IList<Google.Apis.YouTube.v3.Data.Video>? videoDetails,
        IndexingContext indexingContext)
    {
        if (videoDetails != null && videoDetails.Any())
        {
            var matchingVideo =
                videoDetails.MinBy(x => Math.Abs((episode.Length - x.GetLength() ?? TimeSpan.Zero).Ticks));
            var searchResult = searchResults.FirstOrDefault(x => x.Id.VideoId == matchingVideo!.Id);
            if (searchResult != null)
            {
                var matchingPair = new FindEpisodeResponse(searchResult, Video: matchingVideo);
                var video = matchingPair.Video!;
                if (IsDurationMatch(episode, video))
                {
                    logger.LogInformation(
                        "Matched episode '{episodeTitle}' and length: '{episodeLength:g}' with episode '{snippetTitle}' having length: '{matchingPairVideoLength:g}'.",
                        episode.Title, episode.Length, matchingPair.SearchResult?.Snippet.Title,
                        matchingPair.Video?.GetLength());
                    return matchingPair;
                }
            }
        }

        return null;
    }

    private bool IsDurationMatch(RedditPodcastPoster.Models.Episode episode, Google.Apis.YouTube.v3.Data.Video video)
    {
        var matchingVideoLength = video.GetLength() ?? TimeSpan.Zero;
        var matchingVideoLengthDifferentTicks = Math.Abs((matchingVideoLength - episode.Length).Ticks);
        var isMatch = matchingVideoLengthDifferentTicks < VideoDurationTolerance.Ticks;
        return isMatch;
    }

    private SearchResult? MatchOnTextCloseness(RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults)
    {
        return FuzzyMatcher.Match(episode.Title, searchResults, x => x.Snippet.Title, MinFuzzyScore);
    }

    private async Task<SearchResult?> MatchOnEpisodeNumberAndDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IndexingContext indexingContext)
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
                    logger.LogInformation("Matched on episode-number '{episodeNumber}'.", episodeNumber);

                    var videoDetails =
                        await videoService.GetVideoContentDetails(youTubeService,
                            searchResults.Select(x => x.Id.VideoId).ToList(),
                            indexingContext);
                    var video = videoDetails?.SingleOrDefault();
                    if (video == null)
                    {
                        return null;
                    }

                    if (IsDurationMatch(episode, video))
                    {
                        if (IsDurationMatch(episode, video))
                        {
                            logger.LogInformation(
                                "Matched episode '{episodeTitle}' and length: '{episodeLength:g}' with episode '{snippetTitle}' having length: '{matchingPairVideoLength:g}'.",
                                episode.Title, episode.Length, matchingSearchResult.Single().Snippet.Title,
                                video.GetLength());
                            return matchingSearchResult.Single();
                        }
                    }
                }

                if (matchingSearchResult.Any())
                {
                    logger.LogInformation(
                        "Could not match on number that appears in title '{episodeNumber}' as appears in multiple episode-titles: {titles}.",
                        episodeNumber, string.Join(", ", matchingSearchResult.Select(x => $"'{x}'")));
                }
            }
        }

        return null;
    }

    private SearchResult? MatchOnPublishTimeComparedToPublishDelay(
        RedditPodcastPoster.Models.Episode episode,
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

    private SearchResult? MatchOnExactTitle(RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults)
    {
        var episodeTitle = episode.Title.Trim().ToLower();
        var matchingSearchResult = searchResults.Where(x =>
        {
            var snippetTitle = x.Snippet.Title.Trim().ToLower();
            var contains = snippetTitle == episodeTitle ||
                           snippetTitle.Contains(episodeTitle) ||
                           episodeTitle.Contains(snippetTitle);
            return contains;
        });

        if (matchingSearchResult.Count() == 1)
        {
            logger.LogInformation("Matched on episode-number '{episodeTitle}'.", episode.Title);
            return matchingSearchResult.Single();
        }

        if (matchingSearchResult.Any())
        {
            logger.LogInformation(
                "Matched multiple items on episode-title '{episodeTitle}'.", episode.Title);
        }

        return null;
    }

    [GeneratedRegex("(?'number'\\d{2,})", RegexOptions.Compiled)]
    private static partial Regex CreateNumberMatch();
}