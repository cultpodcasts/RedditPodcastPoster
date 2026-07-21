using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery.Adapters;

public interface IEnrichedEpisodeResultsAdapter
{
    IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IAsyncEnumerable<EnrichedEpisodeResult> episodeResults);
}