using System.Xml.Linq;

namespace RedditPodcastPoster.YouTubePushNotifications.Handlers;

public interface IPushNotificationHandler
{
    Task Handle(Guid podcastId, XDocument xml);
}
