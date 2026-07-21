using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery.Services;

public interface IDiscoveryResultDeduplicator
{
    IReadOnlyList<DiscoveryResult> Deduplicate(IEnumerable<DiscoveryResult> results);
}
