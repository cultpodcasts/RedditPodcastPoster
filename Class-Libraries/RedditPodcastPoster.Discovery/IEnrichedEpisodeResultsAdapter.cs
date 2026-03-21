using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public interface IEnrichedEpisodeResultsAdapter
{
    IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IAsyncEnumerable<EnrichedEpisodeResult> episodeResults);
}