using RedditPodcastPoster.PushSubscriptions.Models;

namespace RedditPodcastPoster.PushSubscriptions.Publishers;

public interface INotificationPublisher
{
    public Task SendDiscoveryNotification(DiscoveryNotification discoveryNotification);
}
