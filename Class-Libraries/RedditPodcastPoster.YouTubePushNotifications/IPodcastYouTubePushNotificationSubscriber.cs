using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationSubscriber
{
    Task Renew(Podcast podcast);
    Task Unsubscribe(Podcast podcast);
}