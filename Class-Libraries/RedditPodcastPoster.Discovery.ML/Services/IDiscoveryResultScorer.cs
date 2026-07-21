using RedditPodcastPoster.Discovery.ML.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.ML.Services;

public interface IDiscoveryResultScorer
{
    bool IsEnabled { get; }

    DiscoveryScoreResult Score(DiscoveryResult discoveryResult);
}
