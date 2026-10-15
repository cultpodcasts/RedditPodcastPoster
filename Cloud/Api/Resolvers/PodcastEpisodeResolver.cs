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
                return await ResolveAmbiguousPodcastName(request, podcasts, caller);
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

    /// <summary>
    /// Several podcasts share the route's podcast-name, so the name cannot choose the podcast. The episode-id is unique,
    /// so find the episode by it and compare its podcast with the podcasts carrying the name. A podcast that does not carry
    /// the name means the route and the data disagree, which is an error rather than a not-found.
    /// </summary>
    private async Task<PodcastEpisodeResolverResponse> ResolveAmbiguousPodcastName(
        PodcastEpisodeResolverRequest request,
        IReadOnlyCollection<Podcast> podcastsWithName,
        string caller)
    {
        logger.LogWarning(
            "{method}: {podcastCount} podcasts share the Podcast-Name '{podcastName}' (podcast-ids: {podcastIds}). Resolving Episode-id '{episodeId}' by id.",
            caller, podcastsWithName.Count, request.PodcastName,
            string.Join(", ", podcastsWithName.Select(x => x.Id)), request.EpisodeId);

        var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
        if (episode == null)
        {
            return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
        }

        var owningPodcast = podcastsWithName.SingleOrDefault(x => x.Id == episode.PodcastId);
        if (owningPodcast == null)
        {
            var actualPodcast = await podcastRepository.GetPodcast(episode.PodcastId);
            throw new PodcastEpisodeMismatchException(
                request.EpisodeId, request.PodcastName!, episode.PodcastId, actualPodcast?.Name);
        }

        return new PodcastEpisodeResolverResponse(episode, owningPodcast, PodcastEpisodeResolveState.Resolved);
    }
}