using System.Net;
using Api.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using PushSubscription = Api.Dtos.PushSubscription;

namespace Api;

public class PushSubscriptionController(
    IPushSubscriptionRepository pushSubscriptionRepository,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PushSubscriptionController> logger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PushSubscription")]
    public Task<HttpResponseData> CreatePushSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pushsubscription")]
        HttpRequestData req,
        [FromBody] PushSubscription pushSubscription,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["admin"], pushSubscription, CreatePushSubscription, Unauthorised, ct);
    }

    private async Task<HttpResponseData> CreatePushSubscription(
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
            logger.LogInformation($"Created push-subscription with id '{subscription.Id}' for user '{cp.Subject}'.");
            return req.CreateResponse();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist push-subscription.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}