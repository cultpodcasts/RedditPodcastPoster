using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationUrlAdaptor
{
    (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast);
}