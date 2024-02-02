﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    private const int MinFuzzyScore = 70;
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long BroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;

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
            podcastEpisodes = await applePodcastService.GetEpisodes(applePodcastId, indexingContext);
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                var requestEpisodeTitle = request.EpisodeTitle.Trim();

                var matches = podcastEpisodes.Where(
                    x =>
                    {
                        var trimmedEpisodeTitle = x.Title.Trim();
                        return trimmedEpisodeTitle == requestEpisodeTitle ||
                               trimmedEpisodeTitle.Contains(requestEpisodeTitle) ||
                               requestEpisodeTitle.Contains(trimmedEpisodeTitle);
                    });
                var match = matches.MaxBy(x=>x.Title);
                if (match == null)
                {
                    IEnumerable<AppleEpisode> sampleList;
                    if (reducer != null)
                    {
                        sampleList = podcastEpisodes.Where(reducer);
                    }
                    else
                    {
                        sampleList = podcastEpisodes;
                    }

                    var sameLength = sampleList
                        .Where(x => Math.Abs((x.Duration - request.EpisodeLength!.Value).Ticks) <
                                    TimeDifferenceThreshold);
                    if (sameLength.Count() > 1)
                    {
                        return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title);
                    }

                    match = sameLength.SingleOrDefault(x =>
                        FuzzyMatcher.IsMatch(request.EpisodeTitle, x, y => y.Title, MinFuzzyScore));

                    if (match == null)
                    {
                        sameLength = sampleList
                            .Where(x => Math.Abs((x.Duration - request.EpisodeLength!.Value).Ticks) <
                                        BroaderTimeDifferenceThreshold);
                        return FuzzyMatcher.Match(request.EpisodeTitle, sameLength, x => x.Title, MinFuzzyScore);
                    }
                }

                return match;
            }

            logger.LogInformation(
                $"Podcast '{request.PodcastName}' cannot be found on Apple Podcasts.");
        }

        return matchingEpisode;
    }

    public async Task<IList<Episode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        var episodes = await applePodcastService.GetEpisodes(podcastId, indexingContext);

        if (episodes == null)
        {
            return null;
        }

        if (indexingContext.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince.Value).ToList();
        }

        return episodes.Select(x =>
            Episode.FromApple(
                x.Id,
                x.Title.Trim(),
                x.Description.Trim(),
                x.Duration,
                x.Explicit,
                x.Release,
                x.Url)
        ).ToList();
    }
}