using Api.Models;
using Microsoft.Extensions.Logging;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Podcast = Api.Dtos.Podcast;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

public class PodcastGetService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastGetService> logger) : IPodcastGetService
{
    public async Task<PodcastGetResult> GetAsync(PodcastGetRequest podcastGetRequest, CancellationToken c)
    {
        try
        {
            logger.LogInformation("{method}: Get podcast with request '{podcastGetRequest}'.", nameof(GetAsync),
                podcastGetRequest.ToString());
            var podcastResult = await GetPodcast(podcastGetRequest, c);
            if (podcastResult is { RetrievalState: PodcastRetrievalState.Found, Podcast: not null })
            {
                return new PodcastGetResult(PodcastGetStatus.Found, ToDto(podcastResult.Podcast));
            }

            if (podcastResult.RetrievalState == PodcastRetrievalState.NotFound)
            {
                logger.LogError("Unable to find podcast with name '{name}' and episode-id '{episodeId}'.",
                    podcastGetRequest.PodcastName, podcastGetRequest.EpisodeId);
                return new PodcastGetResult(PodcastGetStatus.NotFound);
            }

            if (podcastResult.RetrievalState == PodcastRetrievalState.Conflict)
            {
                if (podcastResult.AmbiguousPodcasts == null)
                {
                    logger.LogError(
                        "Podcast retrieval in conflict-state without ambiguous-podcasts. name: '{name}' and episode-id: '{episodeId}'.",
                        podcastGetRequest.PodcastName, podcastGetRequest.EpisodeId);
                    return new PodcastGetResult(PodcastGetStatus.Conflict);
                }

                logger.LogError("Multiple podcasts with name '{name}' and episode-id '{episodeId}', ids: {ids}.",
                    podcastGetRequest.PodcastName, podcastGetRequest.EpisodeId,
                    string.Join(", ", podcastResult.AmbiguousPodcasts));
                return new PodcastGetResult(PodcastGetStatus.Conflict, AmbiguousPodcasts: podcastResult.AmbiguousPodcasts);
            }

            return new PodcastGetResult(PodcastGetStatus.Failed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to index-podcast.", nameof(GetAsync));
            return new PodcastGetResult(PodcastGetStatus.Failed);
        }
    }

    private static Podcast ToDto(DomainPodcast podcast)
    {
        return new Podcast
        {
            Id = podcast.Id,
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            AppleId = podcast.AppleId,
            YouTubePublishingDelayTimeSpan = podcast.YouTubePublicationOffset.HasValue
                ? TimeSpan.FromTicks(podcast.YouTubePublicationOffset.Value).ToString("g")
                : string.Empty,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle ?? string.Empty,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            DefaultSubject = podcast.DefaultSubject,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes ?? false,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            KnownTerms = podcast.KnownTerms,
            MinimumDuration = podcast.MinimumDuration?.ToString(),
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags
        };
    }

    private async Task<PodcastWrapper> GetPodcast(PodcastGetRequest podcastGetRequest, CancellationToken c)
    {
        if (podcastGetRequest.PodcastId != null)
        {
            var podcast = await podcastRepository.GetPodcast(podcastGetRequest.PodcastId.Value);
            if (podcast == null)
            {
                return new PodcastWrapper(null, PodcastRetrievalState.NotFound);
            }

            return new PodcastWrapper(podcast, PodcastRetrievalState.Found);
        }

        var podcasts = await podcastRepository.GetAllBy(x => x.Name == podcastGetRequest.PodcastName).ToListAsync(c);
        if (!podcasts.Any() && !string.IsNullOrWhiteSpace(podcastGetRequest.PodcastName))
        {
            var lowerName = podcastGetRequest.PodcastName.ToLower();
            podcasts = await podcastRepository.GetAllBy(x => x.Name.ToLower() == lowerName).ToListAsync(c);
        }
        if (!podcasts.Any())
        {
            return new PodcastWrapper(null, PodcastRetrievalState.NotFound);
        }

        if (podcasts.Count == 1)
        {
            return new PodcastWrapper(podcasts.Single(), PodcastRetrievalState.Found);
        }

        if (podcastGetRequest.EpisodeId.HasValue)
        {
            foreach (var candidatePodcast in podcasts)
            {
                var episode =
                    await episodeRepository.GetEpisode(candidatePodcast.Id, podcastGetRequest.EpisodeId.Value);
                if (episode != null)
                {
                    return new PodcastWrapper(candidatePodcast, PodcastRetrievalState.Found);
                }
            }
        }

        var podcastsWithServiceIds = podcasts.Where(x =>
            !string.IsNullOrWhiteSpace(x.SpotifyId) ||
            !string.IsNullOrWhiteSpace(x.YouTubeChannelId) ||
            x.AppleId != null).ToArray();
        if (podcastsWithServiceIds.Length == 1)
        {
            return new PodcastWrapper(podcastsWithServiceIds.Single(), PodcastRetrievalState.Found);
        }

        return new PodcastWrapper(null, PodcastRetrievalState.Conflict, podcasts.Select(p => p.Id));
    }
}
