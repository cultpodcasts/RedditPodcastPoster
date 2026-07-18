using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IDiscoveryInfoContentPublisher
{
    /// <param name="lastSuccessfulDiscoveryBegan">
    /// When set (successful Discover), updates the durable watermark. When null (e.g. curation),
    /// preserves any existing <see cref="DiscoveryInfo.LastSuccessfulDiscoveryBegan"/> from R2.
    /// </param>
    Task<DiscoveryInfo> PublishUnprocessedSummaryAsync(
        DateTime? lastSuccessfulDiscoveryBegan = null,
        CancellationToken cancellationToken = default);
}
