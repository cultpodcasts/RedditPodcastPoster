using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher;

public interface IContentPublisher
{
    Task PublishHomepage();
    Task PublishSubjects();
    Task PublishFlairs();
    Task PublishDiscoveryInfo(DiscoveryInfo discoveryInfo);
}