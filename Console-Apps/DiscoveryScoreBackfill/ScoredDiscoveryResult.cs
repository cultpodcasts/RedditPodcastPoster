using RedditPodcastPoster.Models;

namespace DiscoveryScoreBackfill;

public sealed record ScoredDiscoveryResult(
    Guid DocumentId,
    DateTime DiscoveryBegan,
    DiscoveryResult Result);
