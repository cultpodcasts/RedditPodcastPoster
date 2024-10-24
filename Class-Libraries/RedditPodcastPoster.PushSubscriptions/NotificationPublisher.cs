using System.Text.Json;
using System.Text.Json.Serialization;
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

        var notificationBuilder = new NotificationBuilder()
            .WithTitle("New discovery available")
            .WithBody("Get to work")
            .WithIcon("assets/cultpodcasts.svg")
            .WithAction("Discover", "discover")
            .WithBadge("assets/cultpodcasts-badge.svg")
            .WithImage("assets/sq-image.png")
            .WithRenotify(true)
            .WithRequireInteraction(true)
            .WithSilent(false)
            .WithTag("discovery")
            .WithTimestamp(DateTimeOffset.Now)
            .WithVibrate([200, 100, 200])
            .WithData(new {url = "/discovery"});

        var payloadJson = JsonSerializer.Serialize(notificationBuilder.Build(), new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var vapidDetails = new VapidDetails(
            "mailto:vapid1@educocult.com",
            _pushSubscriptionsOptions.PublicKey,
            _pushSubscriptionsOptions.PrivateKey);

        foreach (var pushSubscription in pushSubscriptions)
        {
            var subscription = new PushSubscription(pushSubscription.Endpoint.ToString(), pushSubscription.P256Dh,
                pushSubscription.Auth);

            try
            {
                await webPushClient.SendNotificationAsync(subscription, payloadJson, vapidDetails);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to send notification.");
            }
        }
    }
}