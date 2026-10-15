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
                // Several podcasts share the name: the (globally unique) episode-id identifies the episode,
                // and the owning podcast must carry the name given in the route.
                return await ResolveAmbiguousPodcastName(request, request.PodcastName, podcasts);
            }

            if (podcasts.Length == 0)
            {
                return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
            }

            podcast = podcasts.Single();
            episode = await episodeRepository.GetEpisode(podcast.Id, request.EpisodeId);
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

        return new PodcastEpisodeResolverResponse(episode, podcast,
            episode == null ? PodcastEpisodeResolveState.EpisodeNotFound : PodcastEpisodeResolveState.Resolved);
    }

    private async Task<PodcastEpisodeResolverResponse> ResolveAmbiguousPodcastName(
        PodcastEpisodeResolverRequest request,
        string routePodcastName,
        IReadOnlyCollection<Podcast> podcastsWithName)
    {
        var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
        if (episode == null)
        {
            return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
        }

        var owner = podcastsWithName.SingleOrDefault(x => x.Id == episode.PodcastId);
        if (owner == null)
        {
            var actualPodcast = await podcastRepository.GetPodcast(episode.PodcastId);
            throw new PodcastEpisodeMismatchException(
                request.EpisodeId, routePodcastName, episode.PodcastId, actualPodcast?.Name);
        }

        return new PodcastEpisodeResolverResponse(episode, owner, PodcastEpisodeResolveState.Resolved);
    }
}
