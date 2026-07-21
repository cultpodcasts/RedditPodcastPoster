using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface IHomepagePublisher
{
    Task<PublishHomepageResult> PublishHomepage();
}
