using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery;

public interface IDiscoveryResultDeduplicator
{
    IReadOnlyList<DiscoveryResult> Deduplicate(IEnumerable<DiscoveryResult> results);
}
