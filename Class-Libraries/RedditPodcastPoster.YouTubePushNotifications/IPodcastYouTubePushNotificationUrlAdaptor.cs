using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationUrlAdaptor
{
    (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast);
}