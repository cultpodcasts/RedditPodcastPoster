namespace RedditPodcastPoster.PushSubscriptions;

public interface INotificationPublisher
{
    public Task SendDiscoveryNotification(DiscoveryNotification discoveryNotification);
}