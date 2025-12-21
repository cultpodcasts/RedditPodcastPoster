using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using PushSubscription = Api.Dtos.PushSubscription;

namespace Api.Handlers;

public class PushSubscriptionHandler(
    IPushSubscriptionRepository pushSubscriptionRepository,
    ILogger<PushSubscriptionHandler>logger) : IPushSubscriptionHandler
{
    public async Task<HttpResponseData> CreatePushSubscription(
        HttpRequestData req,
        PushSubscription pushSubscription,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        if (cp?.Subject == null)
        {
            logger.LogError($"{nameof(CreatePushSubscription)}: No user.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        try
        {
            DateTime? expirationTime = pushSubscription.ExpirationTime.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(pushSubscription.ExpirationTime.Value).DateTime
                : null;
            var subscription = new RedditPodcastPoster.Models.PushSubscription(
                pushSubscription.Endpoint,
                expirationTime,
                pushSubscription.Keys.Auth,
                pushSubscription.Keys.P256dh,
                cp.Subject
            )
            {
                FileKey = FileKeyFactory.GetFileKey(
                    $"ps-{cp.Subject.Replace("|", "-")}-{DateTimeOffset.Now.ToUnixTimeSeconds()}")
            };
            await pushSubscriptionRepository.Save(subscription);
            logger.LogInformation("Created push-subscription with id '{SubscriptionId}' for user '{CpSubject}'.", subscription.Id, cp.Subject);
            return req.CreateResponse();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist push-subscription.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}