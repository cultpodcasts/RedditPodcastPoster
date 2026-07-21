using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPushSubscriptionHandler
{
    Task<HttpResponseData> CreatePushSubscription(
        HttpRequestData req,
        PushSubscription pushSubscription,
        ClientPrincipal? cp,
        CancellationToken c);
}