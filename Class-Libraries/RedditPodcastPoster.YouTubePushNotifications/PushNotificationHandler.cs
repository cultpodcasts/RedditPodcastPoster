using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PushNotificationHandler : IPushNotificationHandler
{
    private readonly ILogger<PushNotificationHandler> _logger;
    private readonly INotificationAdaptor _notificationAdaptor;

    public PushNotificationHandler(
        INotificationAdaptor notificationAdaptor,
        ILogger<PushNotificationHandler> logger)
    {
        _notificationAdaptor = notificationAdaptor;
        _logger = logger;
    }

    public Task Handle(Guid podcastId, XDocument xml)
    {
        try
        {
            var notification = _notificationAdaptor.Adapt(xml);
            var serialisedNotification = JsonSerializer.Serialize(notification);
            _logger.LogInformation($"Notification for podcast with id '{podcastId}': {serialisedNotification}");
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"Failure to handle notification for podcast with id '{podcastId}'.");
        }

        return Task.CompletedTask;
    }
}