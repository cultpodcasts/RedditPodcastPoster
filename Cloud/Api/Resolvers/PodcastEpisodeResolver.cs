using Microsoft.Extensions.Logging;
using Api.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Resolvers;

public class PodcastEpisodeResolver(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastEpisodeResolver> logger
) : IPodcastEpisodeResolver
{
    public async Task<PodcastEpisodeResolverResponse> ResolvePodcast(PodcastEpisodeResolverRequest request,
        string caller)
    {
        Episode? episode;
        Podcast? podcast = null;
        if (request.PodcastName != null)
        {
            var podcasts = await podcastRepository.GetAllBy(x => x.Name == request.PodcastName).ToArrayAsync();
            if (podcasts.Length > 1)
            {
                return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastConflict);
            }
            else if (podcasts.Length == 0)
            {
                return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound);
            }

            episode = await episodeRepository.GetEpisode(podcasts.Single().Id, request.EpisodeId);
            podcast = podcasts.Single();
        }
        else if (request.PodcastId != null)
        {
            var episodeTask = episodeRepository.GetEpisode(request.PodcastId.Value, request.EpisodeId);
            var podcastTask = podcastRepository.GetPodcast(request.PodcastId.Value);
            await Task.WhenAll(episodeTask, podcastTask);
            episode = episodeTask.Result;
            podcast = podcastTask.Result;
        }
        else
        {
            episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
            if (episode != null)
            {
                podcast = await podcastRepository.GetPodcast(episode.PodcastId);
            }

            logger.LogWarning("{method} used without podcast-id. Episode-id: '{episodeId}'.", caller,
                request.EpisodeId);
        }

        return new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
    }
}