using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IHomepagePublisher
{
    Task<PublishHomepageResult> PublishHomepage();
}
