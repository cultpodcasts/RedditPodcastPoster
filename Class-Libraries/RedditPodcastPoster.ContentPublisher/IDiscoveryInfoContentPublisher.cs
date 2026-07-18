using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IDiscoveryInfoContentPublisher
{
    Task<DiscoveryInfo> PublishUnprocessedSummaryAsync(CancellationToken cancellationToken = default);
}
