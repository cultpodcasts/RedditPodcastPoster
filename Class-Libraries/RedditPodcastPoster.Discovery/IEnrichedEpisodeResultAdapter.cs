using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public interface IEnrichedEpisodeResultAdapter
{
    Task<DiscoveryResult> ToDiscoveryResult(EnrichedEpisodeResult episode);
}