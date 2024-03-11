using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface ISearchProvider
{
    Task<IEnumerable<EpisodeResult>> GetEpisodes(
        IndexingContext indexingContext,
        DiscoveryConfig discoveryConfig);
}