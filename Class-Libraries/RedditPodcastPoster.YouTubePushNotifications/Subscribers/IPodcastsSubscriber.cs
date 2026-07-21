namespace RedditPodcastPoster.YouTubePushNotifications.Subscribers;

public interface IPodcastsSubscriber
{
    public Task SubscribePodcasts();
    Task UpdateLease(Guid podcastId, long leaseSeconds);
    Task RemoveLease(Guid podcastId);
}
