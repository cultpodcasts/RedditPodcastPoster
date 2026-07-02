using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    private const int MinFuzzyScore = 65;
    private const int SameLengthMinFuzzyScore = 35;
    private const int MinSameLengthFuzzyScore = 80;
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long BroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;
    private static readonly long YouTubeDiscoveredDurationThreshold = TimeSpan.FromMinutes(5).Ticks;
    private static readonly TimeSpan SameReleaseThreshold = TimeSpan.FromHours(3);
    private static readonly TimeSpan YouTubeDiscoveredReleaseThreshold = TimeSpan.FromHours(12);

    private static long GetDurationThresholdTicks(FindAppleEpisodeRequest request) =>
        request.EnrichingYouTubeDiscoveredEpisode
            ? YouTubeDiscoveredDurationThreshold
            : TimeDifferenceThreshold;

    private static AppleEpisode? MatchYouTubeDiscoveredEpisodeByDuration(
        IList<AppleEpisode> sampleList,
        TimeSpan episodeLength,
        DateTime? released)
    {
        var sameLength = sampleList
            .Where(x => Math.Abs((x.Duration - episodeLength).Ticks) < YouTubeDiscoveredDurationThreshold)
            .ToList();

        if (sameLength.Count == 1)
        {
            return sameLength[0];
        }

        if (sameLength.Count > 1 && released.HasValue)
        {
            return sameLength.MinBy(x => Math.Abs((x.Release - released.Value).Ticks));
        }

        if (released.HasValue)
        {
            var releaseMatches = sampleList
                .Where(x => Math.Abs((x.Release - released.Value).Ticks) < YouTubeDiscoveredReleaseThreshold.Ticks)
                .ToList();
            if (releaseMatches.Count == 1)
            {
                return releaseMatches[0];
            }

            if (releaseMatches.Count > 1)
            {
                return releaseMatches.MinBy(x => Math.Abs((x.Release - released.Value).Ticks));
            }
        }

        return null;
    }

    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode>? podcastEpisodes = null;
        if (request.PodcastAppleId.HasValue)
        {
            var applePodcastId = new ApplePodcastId(request.PodcastAppleId.Value);
            if (request.EpisodeAppleId.HasValue)
            {
                var episode =
                    await applePodcastService.GetEpisode(applePodcastId, request.EpisodeAppleId.Value, indexingContext);
                if (episode != null)
                {
                    podcastEpisodes = [episode];
                }
            }
            else
            {
                podcastEpisodes = await applePodcastService.GetEpisodes(applePodcastId, indexingContext);
            }
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                var requestEpisodeTitle = WebUtility.HtmlDecode(request.EpisodeTitle.Trim());

                var matches = podcastEpisodes.Where(x =>
                {
                    var trimmedEpisodeTitle = WebUtility.HtmlDecode(x.Title.Trim());
                    return trimmedEpisodeTitle == requestEpisodeTitle ||
                           trimmedEpisodeTitle.Contains(requestEpisodeTitle) ||
                           requestEpisodeTitle.Contains(trimmedEpisodeTitle);
                });
                if (reducer != null)
                {
                    matches = matches.Where(reducer);
                }

                var match = matches.MaxBy(x => x.Title);
                if (match == null)
                {
                    IList<AppleEpisode> sampleList;
                    if (reducer != null)
                    {
                        sampleList = podcastEpisodes.Where(reducer).ToList();
                    }
                    else
                    {
                        sampleList = podcastEpisodes.ToList();
                    }

                    if (request.EpisodeLength is { } episodeLength && episodeLength > TimeSpan.Zero)
                    {
                        if (request.EnrichingYouTubeDiscoveredEpisode)
                        {
                            var youTubeDiscoveredMatch = MatchYouTubeDiscoveredEpisodeByDuration(
                                sampleList,
                                episodeLength,
                                request.Released);
                            if (youTubeDiscoveredMatch != null)
                            {
                                return youTubeDiscoveredMatch;
                            }
                        }

                        var durationThreshold = GetDurationThresholdTicks(request);
                        var sameLength = sampleList
                            .Where(x => Math.Abs((x.Duration - episodeLength).Ticks) < durationThreshold)
                            .ToList();

                        if (sameLength.Count > 1)
                        {
                            return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title, MinSameLengthFuzzyScore);
                        }

                        match = sameLength.SingleOrDefault(x => FuzzyMatcher.IsMatch(request.EpisodeTitle, x, y => y.Title, MinFuzzyScore));

                        if (match == null)
                        {
                            if (request.Released.HasValue)
                            {
                                sameLength = sampleList.Where(x =>
                                    Math.Abs((x.Release - request.Released!).Value.Ticks) <
                                    SameReleaseThreshold.Ticks).ToList();
                            }

                            if (request.ReleaseAuthority == Service.YouTube)
                            {
                                sameLength = sampleList.Where(x =>
                                    Math.Abs((x.Duration - episodeLength).Ticks) <
                                    BroaderTimeDifferenceThreshold).ToList();
                            }

                            return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title,
                                SameLengthMinFuzzyScore);
                        }
                    }
                }

                return match;
            }

            logger.LogInformation(
                "Podcast '{RequestPodcastName}' cannot be found on Apple Podcasts.", request.PodcastName);
        }

        return matchingEpisode;
    }
}