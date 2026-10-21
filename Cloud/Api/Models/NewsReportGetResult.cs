using RedditPodcastPoster.Models.News;

namespace Api.Models;

public enum NewsReportGetStatus
{
    Found,
    NotFound,
    Failed
}

public record NewsReportGetResult(
    NewsReportGetStatus Status,
    NewsReport? Report = null);
