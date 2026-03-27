using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationSubscriber
{
    Task Renew(Podcast podcast);
    Task Unsubscribe(Podcast podcast);
}