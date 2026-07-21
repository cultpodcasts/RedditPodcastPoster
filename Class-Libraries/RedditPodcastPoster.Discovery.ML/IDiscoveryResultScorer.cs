using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.ML;

public interface IDiscoveryResultScorer
{
    bool IsEnabled { get; }

    DiscoveryScoreResult Score(DiscoveryResult discoveryResult);
}
