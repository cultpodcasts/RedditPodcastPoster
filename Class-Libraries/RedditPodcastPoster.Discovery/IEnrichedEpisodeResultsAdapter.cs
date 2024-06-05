using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public interface IEnrichedEpisodeResultsAdapter
{
    IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IEnumerable<EnrichedEpisodeResult> episodeResults);
}