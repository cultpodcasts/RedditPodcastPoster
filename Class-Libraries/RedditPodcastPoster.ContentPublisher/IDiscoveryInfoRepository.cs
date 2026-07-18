using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

/// <summary>
/// Reads the durable discovery-info document from content storage (R2 watermark).
/// </summary>
public interface IDiscoveryInfoRepository
{
    /// <summary>
    /// Returns the published discovery-info document, or null when missing.
    /// </summary>
    Task<DiscoveryInfo?> Get(CancellationToken cancellationToken = default);
}
