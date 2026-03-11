using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationUrlAdaptor
{
    (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast);
}