using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public interface IYouTubeQuotaUsageTracker
{
    Task RecordCallAsync(Application application, ApplicationUsage usage, CancellationToken cancellationToken = default);

    Task RecordQuotaHitAsync(
        Application application,
        ApplicationUsage usage,
        YouTubeQuotaOperation operation,
        CancellationToken cancellationToken = default);

    Task RecordQuotaConsumedAsync(
        Application application,
        ApplicationUsage usage,
        YouTubeQuotaOperation operation,
        int quotaUnits,
        CancellationToken cancellationToken = default);

    Task RecordRingExhaustionAsync(CancellationToken cancellationToken = default);

    Task RecordNonQuotaErrorAsync(CancellationToken cancellationToken = default);

    Task RecordPodcastNotIndexedDueToQuotaAsync(CancellationToken cancellationToken = default);

    Task RecordPodcastNotEnrichedDueToQuotaAsync(CancellationToken cancellationToken = default);

    Task<YouTubeQuotaDailyReport> CreateReportAsync(
        DateOnly reportDate,
        string sourceApplication,
        CancellationToken cancellationToken = default);

    Task EnsureHydratedAsync(CancellationToken cancellationToken = default);

    Task FlushToCosmosAsync(CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);
}
