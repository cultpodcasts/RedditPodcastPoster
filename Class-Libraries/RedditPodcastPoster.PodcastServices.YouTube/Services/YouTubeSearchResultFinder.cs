using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public partial class YouTubeSearchResultFinder(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService videoService,
    IEpisodePlatformMatcher platformMatcher,
    ILogger<YouTubeSearchResultFinder> logger)
    : IYouTubeSearchResultFinder
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
        var exactTitleMatch = await MatchOnExactTitleWithDuration(episode, searchResults, indexingContext);
        if (exactTitleMatch != null)
        {
            return exactTitleMatch;
        }

        var episodeNumberMatch = await MatchOnEpisodeNumberWithDuration(episode, searchResults, indexingContext);
        if (episodeNumberMatch != null)
        {
            return episodeNumberMatch;
        }

        var videoDetails =
            await videoService.GetVideoContentDetails(youTubeService, searchResults.Select(x => x.Id.VideoId).ToList(),
                indexingContext);


        var videoMatch = MatchOnEpisodeDuration(episode, searchResults, videoDetails, indexingContext);
        if (videoMatch != null)
        {
            return videoMatch;
        }

        SearchResult? match;
        if (episode.HasAccurateReleaseTime() && youTubePublishDelay.HasValue)
        {
            match = MatchOnPublishTimeComparedToPublishDelay(episode, searchResults, youTubePublishDelay.Value);
            if (match != null && IsPublishDelayCatalogueMatch(episode, match, youTubePublishDelay.Value, videoDetails))
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
                }
            }
        }

        return await MatchOnTextClosenessWithDuration(episode, searchResults, indexingContext);
    }

    private FindEpisodeResponse? MatchOnEpisodeDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IList<Google.Apis.YouTube.v3.Data.Video>? videoDetails,
        IndexingContext indexingContext)
    {
        var videosWithDuration = videoDetails?
            .Where(x => YouTubeVideoDurationMatcher.HasDuration(x.GetLength()))
            .ToList();
        if (videosWithDuration != null && videosWithDuration.Any())
        {
            var matchingVideo =
                videosWithDuration.MinBy(x => Math.Abs((episode.Length - x.GetLength()!.Value).Ticks));
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

    private async Task<FindEpisodeResponse?> MatchOnEpisodeNumberWithDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IndexingContext indexingContext)
    {
        var match = MatchOnEpisodeNumber(episode, searchResults);
        if (match == null)
        {
            return null;
        }

        return await ValidateSearchMatchWithDuration(episode, match, "episode-number", indexingContext);
    }

    private async Task<FindEpisodeResponse?> MatchOnTextClosenessWithDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IndexingContext indexingContext)
    {
        var match = MatchOnTextCloseness(episode, searchResults);
        if (match == null)
        {
            return null;
        }

        return await ValidateSearchMatchWithDuration(episode, match, "fuzzy title", indexingContext);
    }

    private SearchResult? MatchOnEpisodeNumber(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults)
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
                    return matchingSearchResult.Single();
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

    private async Task<FindEpisodeResponse?> MatchOnExactTitleWithDuration(
        RedditPodcastPoster.Models.Episode episode,
        IList<SearchResult> searchResults,
        IndexingContext indexingContext)
    {
        var match = MatchOnExactTitle(episode, searchResults);
        if (match == null)
        {
            return null;
        }

        return await ValidateSearchMatchWithDuration(episode, match, "episode-title", indexingContext);
    }

    private async Task<FindEpisodeResponse?> ValidateSearchMatchWithDuration(
        RedditPodcastPoster.Models.Episode episode,
        SearchResult match,
        string matchKind,
        IndexingContext indexingContext)
    {
        var videoDetails =
            await videoService.GetVideoContentDetails(youTubeService, [match.Id.VideoId], indexingContext);
        var video = videoDetails?.FirstOrDefault();
        if (video == null)
        {
            return null;
        }

        if (!YouTubeVideoDurationMatcher.IsAcceptableDurationMatch(episode.Length, video.GetLength()))
        {
            logger.LogInformation(
                "Rejected {matchKind} match '{episodeTitle}' (length '{episodeLength:g}') with video '{videoTitle}' (length '{videoLength:g}') due to duration mismatch.",
                matchKind, episode.Title, episode.Length, match.Snippet.Title, video.GetLength());
            return null;
        }

        logger.LogInformation("Matched on {matchKind} '{episodeTitle}'.", matchKind, episode.Title);
        return new FindEpisodeResponse(match, Video: video);
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
            return matchingSearchResult.Single();
        }

        if (matchingSearchResult.Any())
        {
            logger.LogInformation("Matched multiple items on episode-title '{episodeTitle}'.", episode.Title);
        }

        return null;
    }

    [GeneratedRegex("(?'number'\\d{2,})", RegexOptions.Compiled)]
    private static partial Regex CreateNumberMatch();

    private bool IsPublishDelayCatalogueMatch(
        RedditPodcastPoster.Models.Episode episode,
        SearchResult match,
        TimeSpan youTubePublishDelay,
        IList<Google.Apis.YouTube.v3.Data.Video>? videoDetails)
    {
        var videoDetail = videoDetails?.SingleOrDefault(x => x.Id == match.Id.VideoId);
        var catalogueEpisode = new RedditPodcastPoster.Models.Episode
        {
            Title = match.Snippet.Title,
            Release = match.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            Length = videoDetail?.GetLength() ?? episode.Length,
            YouTubeId = match.Id.VideoId
        };

        var podcast = new RedditPodcastPoster.Models.Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = youTubePublishDelay.Ticks
        };

        if (!platformMatcher.IsCatalogueMatch(episode, catalogueEpisode, podcast, episodeMatchRegex: null))
        {
            return false;
        }

        if (videoDetail == null)
        {
            return true;
        }

        var matchingVideoLength = videoDetail.GetLength();
        if (!matchingVideoLength.HasValue)
        {
            return false;
        }

        var matchingVideoLengthDifferentTicks =
            Math.Abs((matchingVideoLength.Value - episode.Length).Ticks);
        return matchingVideoLength > MinDurationForPublicationDate &&
               matchingVideoLengthDifferentTicks < VideoDurationToleranceForPublicationDate.Ticks;
    }
}
