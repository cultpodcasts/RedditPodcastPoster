using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PushSubscriptions.Configuration;
using WebPush;

namespace RedditPodcastPoster.PushSubscriptions;

public class NotificationPublisher(
    IPushSubscriptionRepository pushSubscriptionRepository,
    IOptions<PushSubscriptionsOptions> pushSubscriptionsOptions,
    ILogger<NotificationPublisher> logger
) : INotificationPublisher
{
    private readonly PushSubscriptionsOptions _pushSubscriptionsOptions = pushSubscriptionsOptions.Value;

    public async Task SendDiscoveryNotification()
    {
        var pushSubscriptions = await pushSubscriptionRepository.GetAll().ToListAsync();
        var webPushClient = new WebPushClient();

        var payload = new
        {
            notification = new
            {
                title = "New discovery available",
                body = "Get to work!"
            }
        };

        foreach (var pushSubscription in pushSubscriptions)
        {
            var subscription = new PushSubscription(pushSubscription.Endpoint.ToString(), pushSubscription.P256Dh,
                pushSubscription.Auth);
            var vapidDetails = new VapidDetails(
                "mailto:vapid1@educocult.com",
                _pushSubscriptionsOptions.PublicKey,
                _pushSubscriptionsOptions.PrivateKey);

            try
            {
                await webPushClient.SendNotificationAsync(subscription, JsonSerializer.Serialize(payload),
                    vapidDetails);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to send notification.");
            }
        }
    }
}