using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface ISearchProvider
{
    Task<IEnumerable<EpisodeResult>> GetEpisodes(DiscoveryConfig discoveryConfig, IndexingContext indexingContext);
}