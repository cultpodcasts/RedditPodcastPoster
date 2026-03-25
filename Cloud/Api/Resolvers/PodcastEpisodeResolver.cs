using Api.Dtos;
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
    public async Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(PodcastEpisodeResolverRequest request,
        string caller)
    {
        Episode? episode;
        Podcast? podcast = null;
        if (request.PodcastName != null)
        {
            podcast = await podcastRepositoryV2.GetBy(x => x.Name == request.PodcastName);
            if (podcast == null)
            {
                return (null, null);
            }

            episode = await episodeRepository.GetEpisode(podcast.Id, request.EpisodeId);
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

            logger.LogWarning("{method} used without podcast-id. Episode-id: '{episodeId}'.", caller,
                request.EpisodeId);
        }

        return (episode, podcast);
    }

    public Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(
        PodcastEpisodeRequestWrapper podcastEpisodeResolverRequest, string caller)
    {
        return ResolvePodcast(new PodcastEpisodeResolverRequest(
                podcastEpisodeResolverRequest.EpisodeId,
                podcastEpisodeResolverRequest.PodcastId,
                podcastEpisodeResolverRequest.PodcastName),
            caller);
    }

    public Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(
        EpisodePublishRequestWrapper podcastEpisodeResolverRequest, string caller)
    {
        return ResolvePodcast(new PodcastEpisodeResolverRequest(
                podcastEpisodeResolverRequest.EpisodeId,
                podcastEpisodeResolverRequest.PodcastId,
                null),
            caller);
    }

    public Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(
        EpisodeChangeRequestWrapper podcastEpisodeResolverRequest, string caller)
    {
        return ResolvePodcast(
            new PodcastEpisodeResolverRequest(
                podcastEpisodeResolverRequest.EpisodeId,
                podcastEpisodeResolverRequest.PodcastId,
                null),
            caller);
    }
}