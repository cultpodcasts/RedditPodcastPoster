using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public interface IYouTubeQuotaUsageTracker
{
    void RecordCall(Application application, ApplicationUsage usage);

    void RecordQuotaHit(Application application, ApplicationUsage usage);

    void RecordQuotaConsumed(Application application, ApplicationUsage usage, int quotaUnits);

    YouTubeQuotaDailyReport CreateReport(DateOnly reportDate, string sourceApplication);

    void Reset();
}
