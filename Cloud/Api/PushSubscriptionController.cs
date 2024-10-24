using System.Net;
using Api.Auth;
using Api.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using PushSubscription = Api.Dtos.PushSubscription;

namespace Api;

public class PushSubscriptionController(
    IPushSubscriptionRepository pushSubscriptionRepository,
    ILogger<PushSubscriptionController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
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
        try
        {
            DateTime? expirationTime = pushSubscription.ExpirationTime.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(pushSubscription.ExpirationTime.Value).DateTime
                : null;
            var subscription = new RedditPodcastPoster.Models.PushSubscription(
                pushSubscription.Endpoint,
                expirationTime,
                pushSubscription.Keys.Auth,
                pushSubscription.Keys.P256dh
            )
            {
                FileKey = FileKeyFactory.GetFileKey("push-subscription")
            };
            await pushSubscriptionRepository.Save(subscription);
            return req.CreateResponse();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist push-subscription.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}