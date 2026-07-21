using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications.Subscribers;

public interface IPodcastYouTubePushNotificationSubscriber
{
    Task Renew(Podcast podcast);
    Task Unsubscribe(Podcast podcast);
}
