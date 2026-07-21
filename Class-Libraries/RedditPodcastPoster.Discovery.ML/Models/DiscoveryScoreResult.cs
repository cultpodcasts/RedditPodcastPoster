namespace RedditPodcastPoster.Discovery.ML.Models;

public sealed record DiscoveryScoreResult(float AcceptProbability, bool ShouldAutoHide);
