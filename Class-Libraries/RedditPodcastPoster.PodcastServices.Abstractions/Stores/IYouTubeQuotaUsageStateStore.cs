using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Stores;

public interface IYouTubeQuotaUsageStateStore
{
    Task<YouTubeQuotaUsageState?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(YouTubeQuotaUsageState state, CancellationToken cancellationToken = default);
}
