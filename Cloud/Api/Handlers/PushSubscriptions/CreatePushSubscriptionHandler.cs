using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Models;
using Api.Services.PushSubscriptions;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.PushSubscriptions;

public class CreatePushSubscriptionHandler(
    IPushSubscriptionCreateService pushSubscriptionCreateService,
    ILogger<CreatePushSubscriptionHandler> logger) : ICreatePushSubscriptionHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PushSubscription pushSubscription,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await pushSubscriptionCreateService.CreateAsync(pushSubscription, cp?.Subject, c);
        return result.Status switch
        {
            PushSubscriptionCreateStatus.Created =>
                req.CreateResponse(),
            PushSubscriptionCreateStatus.NoUser or PushSubscriptionCreateStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Push subscription create failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
