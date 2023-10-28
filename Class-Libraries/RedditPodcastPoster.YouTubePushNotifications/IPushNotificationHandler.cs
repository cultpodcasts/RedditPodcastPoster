using System.Xml.Linq;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface IPushNotificationHandler
{
    Task Handle(Guid podcastId, XDocument xml);
}