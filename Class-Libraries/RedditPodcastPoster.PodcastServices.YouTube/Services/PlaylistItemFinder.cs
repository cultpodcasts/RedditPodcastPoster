using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public partial class PlaylistItemFinder(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService videoService,
    IEpisodePlatformMatcher platformMatcher,
    ILogger<PlaylistItemFinder> logger)
    : IPlaylistItemFinder
{
    private const int MinFuzzyScore = 70;
    private static readonly Regex NumberMatch = CreateNumberMatch();
    private static readonly TimeSpan VideoDurationTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MinDurationForPublicationDate = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VideoDurationToleranceForPublicationDate = TimeSpan.FromMinutes(5);

    public async Task<FindEpisodeResponse?> FindMatchingYouTubeVideo(
        EpisodeModel episode,
        IList<PlaylistItem> playlistItems,
        TimeSpan? youTubePublishDelay,
        IndexingContext indexingContext)
    {
        playlistItems = await ExcludeLiveAndUpcoming(playlistItems, indexingContext);
        if (!playlistItems.Any())
        {
            return null;
        }

        var exactTitleMatch = await MatchOnExactTitleWithDuration(episode, playlistItems, indexingContext);
        if (exactTitleMatch != null)
        {
            return exactTitleMatch;
        }

        var episodeNumberMatch = await MatchOnEpisodeNumberWithDuration(episode, playlistItems, indexingContext);
        if (episodeNumberMatch != null)
        {
            return episodeNumberMatch;
        }

        var videoIds = playlistItems.Select(x => x.GetVideoId()).ToList();
        var videoDetails = await videoService.GetVideoContentDetails(youTubeService, videoIds, indexingContext);


        var videoMatch = MatchOnEpisodeDuration(episode, playlistItems, videoDetails, indexingContext);
        if (videoMatch != null)
        {
            return videoMatch;
        }

        PlaylistItem? match;
        if (episode.HasAccurateReleaseTime() && youTubePublishDelay.HasValue)
        {
            match = MatchOnPublishTimeComparedToPublishDelay(episode, playlistItems, youTubePublishDelay.Value);
            if (match != null && IsPublishDelayCatalogueMatch(episode, match, youTubePublishDelay.Value, videoDetails))
            {
                if (videoDetails != null)
                {
                    // verify duration is similar
                    var videoDetail = videoDetails.SingleOrDefault(x => x.Id == match.GetVideoId());
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
                                return new FindEpisodeResponse(PlaylistItem: match);
                            }
                        }
                    }
                }
            }
        }

        return await MatchOnTextClosenessWithDuration(episode, playlistItems, indexingContext);
    }

    private FindEpisodeResponse? MatchOnEpisodeDuration(
        EpisodeModel episode,
        IList<PlaylistItem> searchResults,
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
            var searchResult = searchResults.FirstOrDefault(x => x.GetVideoId() == matchingVideo!.Id);
            if (searchResult != null)
            {
                var matchingPair = new FindEpisodeResponse(PlaylistItem: searchResult, Video: matchingVideo);
                var matchingVideoLength = matchingPair.Video!.GetLength() ?? TimeSpan.Zero;
                var matchingVideoLengthDifferentTicks = Math.Abs((matchingVideoLength - episode.Length).Ticks);
                if (matchingVideoLengthDifferentTicks < VideoDurationTolerance.Ticks)
                {
                    logger.LogInformation(
                        "Matched episode '{episodeTitle}' and length: '{episodeLength:g}' with episode '{snippetTitle}' having length: '{videoLength:g}'.",
                        episode.Title, episode.Length, searchResult.Snippet?.Title,
                        matchingPair.Video?.GetLength());
                    return matchingPair;
                }
            }
        }

        return null;
    }

    private PlaylistItem? MatchOnTextCloseness(EpisodeModel episode,
        IList<PlaylistItem> searchResults)
    {
        return FuzzyMatcher.Match(episode.Title, searchResults, x => x.Snippet.Title, MinFuzzyScore);
    }

    private PlaylistItem? MatchOnEpisodeNumber(EpisodeModel episode,
        IList<PlaylistItem> searchResults)
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

    private PlaylistItem? MatchOnPublishTimeComparedToPublishDelay(
        EpisodeModel episode,
        IList<PlaylistItem> searchResults,
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

    private async Task<FindEpisodeResponse?> MatchOnEpisodeNumberWithDuration(
        EpisodeModel episode,
        IList<PlaylistItem> playlistItems,
        IndexingContext indexingContext)
    {
        var match = MatchOnEpisodeNumber(episode, playlistItems);
        if (match == null)
        {
            return null;
        }

        return await ValidatePlaylistMatchWithDuration(episode, match, "episode-number", indexingContext);
    }

    private async Task<FindEpisodeResponse?> MatchOnTextClosenessWithDuration(
        EpisodeModel episode,
        IList<PlaylistItem> playlistItems,
        IndexingContext indexingContext)
    {
        var match = MatchOnTextCloseness(episode, playlistItems);
        if (match == null)
        {
            return null;
        }

        return await ValidatePlaylistMatchWithDuration(episode, match, "fuzzy title", indexingContext);
    }

    private async Task<FindEpisodeResponse?> MatchOnExactTitleWithDuration(
        EpisodeModel episode,
        IList<PlaylistItem> playlistItems,
        IndexingContext indexingContext)
    {
        var match = MatchOnExactTitle(episode, playlistItems);
        if (match == null)
        {
            return null;
        }

        return await ValidatePlaylistMatchWithDuration(episode, match, "episode-title", indexingContext);
    }

    private async Task<FindEpisodeResponse?> ValidatePlaylistMatchWithDuration(
        EpisodeModel episode,
        PlaylistItem match,
        string matchKind,
        IndexingContext indexingContext)
    {
        var videoDetails = await videoService.GetVideoContentDetails(
            youTubeService, [match.GetVideoId()], indexingContext);
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
        return new FindEpisodeResponse(PlaylistItem: match, Video: video);
    }

    private async Task<IList<PlaylistItem>> ExcludeLiveAndUpcoming(
        IList<PlaylistItem> playlistItems,
        IndexingContext indexingContext)
    {
        var videoIds = playlistItems.Select(x => x.GetVideoId()).ToList();
        var videos = await videoService.GetVideoContentDetails(
            youTubeService, videoIds, indexingContext, withSnippets: true);
        if (videos == null)
        {
            return playlistItems;
        }

        var completedVideoIds = videos
            .Where(x => x.IsCompletedPublicVideo())
            .Select(x => x.Id)
            .ToHashSet();
        return playlistItems.Where(x => completedVideoIds.Contains(x.GetVideoId())).ToList();
    }

    private PlaylistItem? MatchOnExactTitle(EpisodeModel episode,
        IList<PlaylistItem> searchResults)
    {
        var episodeTitle = episode.Title.Trim().ToLower();
        var matchingSearchResult = searchResults.Where(x =>
        {
            var snippetTitle = x.Snippet.Title.Trim().ToLower();
            return snippetTitle == episodeTitle ||
                   snippetTitle.Contains(episodeTitle) ||
                   episodeTitle.Contains(snippetTitle);
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

    [GeneratedRegex("(?'number'\\d+)")]
    private static partial Regex CreateNumberMatch();

    private bool IsPublishDelayCatalogueMatch(
        EpisodeModel episode,
        PlaylistItem match,
        TimeSpan youTubePublishDelay,
        IList<Google.Apis.YouTube.v3.Data.Video>? videoDetails)
    {
        var videoDetail = videoDetails?.SingleOrDefault(x => x.Id == match.GetVideoId());
        var catalogueEpisode = new EpisodeModel
        {
            Title = match.Snippet.Title,
            Release = match.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            Length = videoDetail?.GetLength() ?? episode.Length,
            YouTubeId = match.GetVideoId()
        };

        var podcast = new Podcast
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
