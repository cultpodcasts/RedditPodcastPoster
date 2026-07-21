using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.Services;

public interface IDiscoveryResultDeduplicator
{
    IReadOnlyList<DiscoveryResult> Deduplicate(IEnumerable<DiscoveryResult> results);
}
