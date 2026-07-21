using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPodcastYouTubePushNotificationUrlAdaptor
{
    (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast);
}