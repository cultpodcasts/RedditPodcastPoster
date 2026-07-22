using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.PushSubscriptions;

public interface IPushSubscriptionCreateService
{
    Task<PushSubscriptionCreateResult> CreateAsync(
        PushSubscription pushSubscription,
        string? subject,
        CancellationToken cancellationToken);
}

public class PushSubscriptionCreateService(
    IPushSubscriptionRepository pushSubscriptionRepository,
    ILogger<PushSubscriptionCreateService> logger) : IPushSubscriptionCreateService
{
    public async Task<PushSubscriptionCreateResult> CreateAsync(
        PushSubscription pushSubscription,
        string? subject,
        CancellationToken cancellationToken)
    {
        if (subject == null)
        {
            logger.LogError($"{nameof(CreateAsync)}: No user.");
            return new PushSubscriptionCreateResult(PushSubscriptionCreateStatus.NoUser);
        }

        try
        {
            DateTime? expirationTime = pushSubscription.ExpirationTime.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(pushSubscription.ExpirationTime.Value).DateTime
                : null;
            var subscription = new RedditPodcastPoster.Models.Notifications.PushSubscription(
                pushSubscription.Endpoint,
                expirationTime,
                pushSubscription.Keys.Auth,
                pushSubscription.Keys.P256dh,
                subject
            )
            {
                FileKey = FileKeyFactory.GetFileKey(
                    $"ps-{subject.Replace("|", "-")}-{DateTimeOffset.Now.ToUnixTimeSeconds()}")
            };
            await pushSubscriptionRepository.Save(subscription);
            logger.LogInformation(
                "Created push-subscription with id '{SubscriptionId}' for user '{Subject}'.",
                subscription.Id, subject);
            return new PushSubscriptionCreateResult(PushSubscriptionCreateStatus.Created);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist push-subscription.");
            return new PushSubscriptionCreateResult(PushSubscriptionCreateStatus.Failed);
        }
    }
}
