using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PushNotificationHandler(
    INotificationAdaptor notificationAdaptor,
    ILogger<PushNotificationHandler> logger)
    : IPushNotificationHandler
{
    public Task Handle(Guid podcastId, XDocument xml)
    {
        try
        {
            var notification = notificationAdaptor.Adapt(xml);
            var serialisedNotification = JsonSerializer.Serialize(notification);
            logger.LogInformation($"Notification for podcast with id '{podcastId}': {serialisedNotification}");
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failure to handle notification for podcast with id '{podcastId}'.");
        }

        return Task.CompletedTask;
    }
}