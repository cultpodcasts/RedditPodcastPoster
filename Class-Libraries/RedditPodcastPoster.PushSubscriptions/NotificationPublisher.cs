using System.Net;
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
            _pushSubscriptionsOptions.Subject,
            _pushSubscriptionsOptions.PublicKey,
            _pushSubscriptionsOptions.PrivateKey);
        var sent = 0;
        foreach (var pushSubscription in pushSubscriptions)
        {
            var subscription = new PushSubscription(pushSubscription.Endpoint.ToString(), pushSubscription.P256Dh,
                pushSubscription.Auth);

            try
            {
                await webPushClient.SendNotificationAsync(subscription, payloadJson, vapidDetails);
                logger.LogInformation($"Notification sent to '{pushSubscription.User}'.");
                sent++;
            }
            catch (WebPushException ex)
            {
                if (ex.HttpResponseMessage.StatusCode == HttpStatusCode.Gone)
                {
                    logger.LogError(ex, $"Subscription with id '{pushSubscription.Id}' has gone.");
                    try
                    {
                        await pushSubscriptionRepository.Delete(pushSubscription);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"Failure to delete push-subscription with id '{pushSubscription.Id}'.");
                    }
                }
                else
                {
                    logger.LogError(ex, "Failure to send notification.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to send notification.");
            }
        }

        var plural = "s";
        if (pushSubscriptions.Count == 1)
        {
            plural = string.Empty;
        }

        if (sent < pushSubscriptions.Count)
        {
            logger.LogWarning($"Sent {sent}/{pushSubscriptions.Count} push-notification{plural}.");
        }
        else
        {
            logger.LogWarning($"Sent {sent} push-notification{plural}.");
        }
    }
}