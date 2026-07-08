using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IYouTubeQuotaUsageStateStore
{
    Task<YouTubeQuotaUsageState?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(YouTubeQuotaUsageState state, CancellationToken cancellationToken = default);
}
