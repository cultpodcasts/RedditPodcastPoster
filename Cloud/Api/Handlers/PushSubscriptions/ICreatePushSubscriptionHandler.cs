using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.PushSubscriptions;

public interface ICreatePushSubscriptionHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PushSubscription pushSubscription,
        ClientPrincipal? cp,
        CancellationToken c);
}
