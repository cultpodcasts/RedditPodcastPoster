using System.Xml.Linq;
using RedditPodcastPoster.YouTubePushNotifications.Models;

namespace RedditPodcastPoster.YouTubePushNotifications.Adaptors;

public interface INotificationAdaptor
{
    Notification Adapt(XDocument xml);
}
