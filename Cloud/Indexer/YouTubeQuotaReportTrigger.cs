using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace Indexer;

public class YouTubeQuotaReportTrigger(
    IYouTubeQuotaUsageTracker quotaUsageTracker,
    ILookupRepository lookupRepository,
    ILogger<YouTubeQuotaReportTrigger> logger)
{
    private const string SourceApplication = "Indexer";

    [Function("YouTubeQuotaReport")]
    public async Task Run(
        [TimerTrigger("0 55 6 * * *"
#if DEBUG
            , RunOnStartup = false
#endif
        )]
        TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        var reportDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        logger.LogInformation(
            "Flushing YouTube quota usage report for Pacific quota day {ReportDate} from {SourceApplication}.",
            reportDate,
            SourceApplication);

        var report = await quotaUsageTracker.CreateReportAsync(reportDate, SourceApplication, cancellationToken);
        await lookupRepository.SaveYouTubeQuotaDailyReport(report);
        await quotaUsageTracker.ResetAsync(cancellationToken);

        logger.LogInformation(
            "Saved YouTube quota report {ReportId} with {KeyCount} keys, {UsedIndexerKeyCount} used indexer keys, {UnusedIndexerKeyCount} unused indexer keys, {PodcastsNotIndexedDueToQuota} podcasts not indexed due to quota, {PodcastsNotEnrichedDueToQuota} podcasts not enriched due to quota, {RingExhaustionCount} ring exhaustions, {NonQuotaErrorCount} non-quota errors.",
            report.Id,
            report.Keys.Count,
            report.UsedIndexerKeys.Count,
            report.UnusedIndexerKeys.Count,
            report.PodcastsNotIndexedDueToQuota,
            report.PodcastsNotEnrichedDueToQuota,
            report.RingExhaustionCount,
            report.NonQuotaErrorCount);
    }
}
