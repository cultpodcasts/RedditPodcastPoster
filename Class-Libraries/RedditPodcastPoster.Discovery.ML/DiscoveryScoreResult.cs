namespace RedditPodcastPoster.Discovery.ML;

public sealed record DiscoveryScoreResult(float AcceptProbability, bool ShouldAutoHide);
