using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Discovery;

public interface IEpisodeResultsAdapter
{
    IAsyncEnumerable<DiscoveryResult> ToDiscoveryResults(IEnumerable<EpisodeResult> episodeResults);
}