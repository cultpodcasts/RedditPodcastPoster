using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using DomainPodcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

public class PodcastEpisodeProjectionHelper(IEpisodeRepository episodeRepository)
{
    public async Task HydrateDetachedEpisodePodcastProjection(DomainPodcast podcast, CancellationToken c)
    {
        await foreach (var episode in episodeRepository.GetByPodcastId(podcast.Id).WithCancellation(c))
        {
            episode.PodcastName = podcast.Name;
            episode.PodcastSearchTerms = podcast.SearchTerms;
            episode.PodcastRemoved = podcast.Removed;
            await episodeRepository.Save(episode);
        }
    }

    public async Task<List<Guid>> GetEpisodeIdsByPodcastId(Guid podcastId, CancellationToken c)
    {
        var ids = new List<Guid>();
        await foreach (var episode in episodeRepository.GetByPodcastId(podcastId).WithCancellation(c))
        {
            ids.Add(episode.Id);
        }

        return ids;
    }

    public async Task<List<Episode>> GetDetachedEpisodesByPodcastId(Guid podcastId, CancellationToken c)
    {
        var episodes = new List<Episode>();
        await foreach (var episode in episodeRepository.GetByPodcastId(podcastId).WithCancellation(c))
        {
            episodes.Add(episode);
        }

        return episodes;
    }
}
