using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.Discovery.Providers;

public interface ISearchProvider
{
    Task<IEnumerable<EpisodeResult>> GetEpisodes(DiscoveryConfig discoveryConfig, IndexingContext indexingContext);
}
