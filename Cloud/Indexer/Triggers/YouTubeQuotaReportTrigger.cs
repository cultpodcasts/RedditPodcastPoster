using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Indexer.Models;
using Indexer.Orchestrations;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace Indexer.Triggers;

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

        var rollingReport = await lookupRepository.GetYouTubeQuotaReport()
                            ?? new YouTubeQuotaReport {SourceApplication = SourceApplication};
        rollingReport.UpsertDay(report);
        rollingReport.UpdatedUtc = DateTime.UtcNow;
        await lookupRepository.SaveYouTubeQuotaReport(rollingReport);

        await quotaUsageTracker.ResetAsync(cancellationToken);

        logger.LogInformation(
            "Saved YouTube quota report {ReportId} holding {DayCount} days (latest {ReportDate}) with {KeyCount} keys, {UsedIndexerKeyCount} used indexer keys, {UnusedIndexerKeyCount} unused indexer keys, {PodcastsNotIndexedDueToQuota} podcasts not indexed due to quota, {PodcastsNotEnrichedDueToQuota} podcasts not enriched due to quota, {RingExhaustionCount} ring exhaustions, {NonQuotaErrorCount} non-quota errors.",
            rollingReport.Id,
            rollingReport.Days.Count,
            report.ReportDate,
            report.Keys.Count,
            report.UsedIndexerKeys.Count,
            report.UnusedIndexerKeys.Count,
            report.PodcastsNotIndexedDueToQuota,
            report.PodcastsNotEnrichedDueToQuota,
            report.RingExhaustionCount,
            report.NonQuotaErrorCount);
    }
}
