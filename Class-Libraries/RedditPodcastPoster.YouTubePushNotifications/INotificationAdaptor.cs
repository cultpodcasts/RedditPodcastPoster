using System.Xml.Linq;
using RedditPodcastPoster.YouTubePushNotifications.Models;

namespace RedditPodcastPoster.YouTubePushNotifications;

public interface INotificationAdaptor
{
    Notification Adapt(XDocument xml);
}