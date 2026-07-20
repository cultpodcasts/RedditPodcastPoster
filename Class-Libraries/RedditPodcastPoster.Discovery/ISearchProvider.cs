using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery;

public interface ISearchProvider
{
    Task<IEnumerable<EpisodeResult>> GetEpisodes(DiscoveryConfig discoveryConfig, IndexingContext indexingContext);
}
