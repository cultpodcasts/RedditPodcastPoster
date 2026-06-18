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
        var reportDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-1));
        logger.LogInformation(
            "Flushing YouTube quota usage report for {ReportDate} from {SourceApplication}.",
            reportDate,
            SourceApplication);

        var report = quotaUsageTracker.CreateReport(reportDate, SourceApplication);
        await lookupRepository.SaveYouTubeQuotaDailyReport(report);
        quotaUsageTracker.Reset();

        logger.LogInformation(
            "Saved YouTube quota report {ReportId} with {KeyCount} keys.",
            report.Id,
            report.Keys.Count);
    }
}
