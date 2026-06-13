using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Discovery.ML;

public interface IDiscoveryResultScorer
{
    bool IsEnabled { get; }

    DiscoveryScoreResult Score(DiscoveryResult discoveryResult);
}
