using RedditPodcastPoster.ContentPublisher.Models;

namespace Api.Models;

public enum HomepagePublishStatus
{
    Ok,
    Failed
}

public record HomepagePublishResult(
    HomepagePublishStatus Status,
    PublishHomepageResult? Result = null);
