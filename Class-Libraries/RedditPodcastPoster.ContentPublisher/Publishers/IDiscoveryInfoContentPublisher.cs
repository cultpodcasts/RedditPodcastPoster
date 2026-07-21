using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface IDiscoveryInfoContentPublisher
{
    Task<DiscoveryInfo> PublishUnprocessedSummaryAsync(CancellationToken cancellationToken = default);
}
