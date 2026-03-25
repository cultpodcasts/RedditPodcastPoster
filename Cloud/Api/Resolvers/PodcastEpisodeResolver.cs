using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace Api.Resolvers;

public class PodcastEpisodeResolver(
    IPodcastRepositoryV2 podcastRepositoryV2,
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
            var podcasts= await podcastRepositoryV2.GetAllBy(x => x.Name == request.PodcastName).ToArrayAsync();
            if (podcasts.Length > 1)
            {
                return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastConflict);
            }
            else if (podcasts.Length==0) {
                return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.PodcastNotFound);
            }

            episode = await episodeRepository.GetEpisode(podcasts.Single().Id, request.EpisodeId);
        }
        else if (request.PodcastId != null)
        {
            episode = await episodeRepository.GetEpisode(request.PodcastId.Value, request.EpisodeId);
            podcast = await podcastRepositoryV2.GetPodcast(request.PodcastId.Value);
        }
        else
        {
            episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
            if (episode != null)
            {
                podcast = await podcastRepositoryV2.GetPodcast(episode.PodcastId);
            }

            logger.LogWarning("{method} used without podcast-id. Episode-id: '{episodeId}'.", caller, request.EpisodeId);
        }

        return new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
    }
}