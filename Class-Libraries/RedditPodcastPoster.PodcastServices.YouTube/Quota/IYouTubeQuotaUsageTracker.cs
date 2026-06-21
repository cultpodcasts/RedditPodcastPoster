using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public interface IYouTubeQuotaUsageTracker
{
    Task RecordCallAsync(Application application, ApplicationUsage usage, CancellationToken cancellationToken = default);

    Task RecordQuotaHitAsync(Application application, ApplicationUsage usage, CancellationToken cancellationToken = default);

    Task RecordQuotaConsumedAsync(
        Application application,
        ApplicationUsage usage,
        int quotaUnits,
        CancellationToken cancellationToken = default);

    Task<YouTubeQuotaDailyReport> CreateReportAsync(
        DateOnly reportDate,
        string sourceApplication,
        CancellationToken cancellationToken = default);

    Task EnsureHydratedAsync(CancellationToken cancellationToken = default);

    Task FlushToCosmosAsync(CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
