using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace RedditPodcastPoster.YouTubePushNotifications.Adaptors;

public interface IPodcastYouTubePushNotificationUrlAdaptor
{
    (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast);
}
