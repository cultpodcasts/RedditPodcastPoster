using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PushSubscriptions.Configuration;
using RedditPodcastPoster.PushSubscriptions.Dtos;
using WebPush;

namespace RedditPodcastPoster.PushSubscriptions;

public class NotificationPublisher(
    IPushSubscriptionRepository pushSubscriptionRepository,
    IOptions<PushSubscriptionsOptions> pushSubscriptionsOptions,
    ILogger<NotificationPublisher> logger
) : INotificationPublisher
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = {new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)}
    };

    private readonly PushSubscriptionsOptions _pushSubscriptionsOptions = pushSubscriptionsOptions.Value;

    public async Task SendDiscoveryNotification(DiscoveryNotification discoveryNotification)
    {
        var pushSubscriptions = await pushSubscriptionRepository.GetAll().ToListAsync();
        var webPushClient = new WebPushClient();

        var notificationBuilder = new NotificationBuilder()
            .WithTitle("New discovery available")
            .WithBody(discoveryNotification.ToString())
            .WithIcon("assets/cultpodcasts.svg")
            .WithDefaultAction(ActionOperation.NavigateLastFocusedOrOpen, new Uri("/discovery", UriKind.Relative))
            .WithAction("Discover", "discover", actionOperation: ActionOperation.NavigateLastFocusedOrOpen,
                url: new Uri("/discovery", UriKind.Relative))
            .WithBadge("assets/cultpodcasts-badge.svg")
            .WithRenotify(true)
            .WithRequireInteraction(true)
            .WithSilent(false)
            .WithTag("discovery")
            .WithTimestamp(DateTimeOffset.Now)
            .WithVibrate([200, 100, 200]);

        var payloadJson = JsonSerializer.Serialize(notificationBuilder.Build(), JsonSerializerOptions);

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
                logger.LogInformation("Notification sent to '{PushSubscriptionUser}'.", pushSubscription.User);
                sent++;
            }
            catch (WebPushException ex)
            {
                if (ex.HttpResponseMessage.StatusCode == HttpStatusCode.Gone)
                {
                    logger.LogError(ex, "Subscription with id '{PushSubscriptionId}' has gone.", pushSubscription.Id);
                    try
                    {
                        await pushSubscriptionRepository.Delete(pushSubscription);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failure to delete push-subscription with id '{PushSubscriptionId}'.", pushSubscription.Id);
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
            logger.LogWarning("Sent {Sent}/{PushSubscriptionsCount} push-notification{Plural}.", sent, pushSubscriptions.Count, plural);
        }
        else
        {
            logger.LogWarning("Sent {Sent} push-notification{Plural}.", sent, plural);
        }
    }
}