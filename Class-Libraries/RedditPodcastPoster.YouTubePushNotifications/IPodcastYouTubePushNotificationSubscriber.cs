using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationSubscriber
{
    Task Renew(Podcast podcast);
}