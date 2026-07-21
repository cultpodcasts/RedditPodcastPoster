using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.Adapters;

public interface IEnrichedEpisodeResultAdapter
{
    Task<DiscoveryResult> ToDiscoveryResult(EnrichedEpisodeResult episode);
}