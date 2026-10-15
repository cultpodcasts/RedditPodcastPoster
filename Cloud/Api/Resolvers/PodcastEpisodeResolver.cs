using Microsoft.Extensions.Logging;
using Api.Models;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Resolvers;

/// <summary>
/// Resolves an episode and its owning podcast. An episode-id is globally unique, so it always decides:
/// a podcast-id or podcast-name supplied alongside it is only a hint used to narrow the search
/// (point reads within the hinted podcast partitions) and never a reason to fail or report a conflict.
/// </summary>
public class PodcastEpisodeResolver(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastEpisodeResolver> logger
) : IPodcastEpisodeResolver
{
    public async Task<PodcastEpisodeResolverResponse> ResolvePodcast(PodcastEpisodeResolverRequest request,
        string caller)
    {
        var hintedPodcasts = new Dictionary<Guid, Podcast>();
        var hintedPodcastIds = new List<Guid>();
        if (request.PodcastId != null)
        {
            hintedPodcastIds.Add(request.PodcastId.Value);
        }
        else if (request.PodcastName != null)
        {
            await foreach (var podcast in podcastRepository.GetAllBy(x => x.Name == request.PodcastName))
            {
                hintedPodcasts[podcast.Id] = podcast;
                hintedPodcastIds.Add(podcast.Id);
            }
        }

        foreach (var hintedPodcastId in hintedPodcastIds)
        {
            var hintedEpisode = await episodeRepository.GetEpisode(hintedPodcastId, request.EpisodeId);
            if (hintedEpisode != null)
            {
                return await Resolved(hintedEpisode, hintedPodcasts);
            }
        }

        if (hintedPodcastIds.Count == 0)
        {
            logger.LogWarning("{method} used without podcast-id or podcast-name. Episode-id: '{episodeId}'.", caller,
                request.EpisodeId);
        }
        else
        {
            logger.LogWarning(
                "{method}: Episode-id '{episodeId}' not found within hinted podcasts (podcast-id: '{podcastId}', podcast-name: '{podcastName}'). Searching by episode-id.",
                caller, request.EpisodeId, request.PodcastId, request.PodcastName);
        }

        var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
        if (episode == null)
        {
            return new PodcastEpisodeResolverResponse(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
        }

        return await Resolved(episode, hintedPodcasts);
    }

    private async Task<PodcastEpisodeResolverResponse> Resolved(Episode episode,
        IReadOnlyDictionary<Guid, Podcast> knownPodcasts)
    {
        var podcast = knownPodcasts.TryGetValue(episode.PodcastId, out var known)
            ? known
            : await podcastRepository.GetPodcast(episode.PodcastId);
        return new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
    }
}
