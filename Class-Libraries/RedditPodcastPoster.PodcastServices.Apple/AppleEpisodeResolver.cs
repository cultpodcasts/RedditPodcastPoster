﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeResolver : IAppleEpisodeResolver
{
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromMinutes(1).Ticks;
    private readonly ICachedApplePodcastService _applePodcastService;
    private readonly ILogger<AppleEpisodeResolver> _logger;

    public AppleEpisodeResolver(
        ICachedApplePodcastService applePodcastService,
        ILogger<AppleEpisodeResolver> logger)
    {
        _applePodcastService = applePodcastService;
        _logger = logger;
    }

    public async Task<AppleEpisode?> FindEpisode(FindAppleEpisodeRequest request, IndexingContext indexingContext)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode>? podcastEpisodes = null;
        if (request.PodcastAppleId.HasValue)
        {
            var applePodcastId = new ApplePodcastId(request.PodcastAppleId.Value);
            podcastEpisodes = await _applePodcastService.GetEpisodes(applePodcastId, indexingContext);
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null)
        {
            if (request.PodcastAppleId.HasValue)
            {
                var matchingEpisodes = podcastEpisodes.Where(x => x.Title == request.EpisodeTitle);
                if (!matchingEpisodes.Any() || matchingEpisodes.Count() > 1)
                {
                    IEnumerable<AppleEpisode> matches;
                    if (request is {ReleaseAuthority: Service.YouTube, EpisodeLength: not null} ||
                        !request.Released.HasValue)
                    {
                        matches = podcastEpisodes.Where(x =>
                            Math.Abs((x.Duration - request.EpisodeLength!.Value).Ticks) < TimeDifferenceThreshold);
                    }
                    else
                    {
                        matches = podcastEpisodes.Where(x =>
                            DateOnly.FromDateTime(x.Release) == DateOnly.FromDateTime(request.Released.Value));
                    }

                    if (matches.Count() > 1)
                    {
                        var distances =
                            matches.OrderByDescending(x =>
                                Levenshtein.CalculateSimilarity(request.EpisodeTitle, x.Title));
                        return distances.FirstOrDefault()!;
                    }

                    matchingEpisode = matches.SingleOrDefault();

                    if (matchingEpisode != null && request.Released.HasValue)
                    {
                        if (Math.Abs((matchingEpisode.Release - request.Released.Value).Ticks) >
                            TimeSpan.FromDays(14).Ticks)
                        {
                            matchingEpisode = null;
                        }
                    }
                }

                matchingEpisode ??= matchingEpisodes.FirstOrDefault();
            }
            else
            {
                _logger.LogInformation(
                    $"Podcast '{request.PodcastName}' cannot be found on Apple Podcasts.");
            }
        }

        return matchingEpisode;
    }
}