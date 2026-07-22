using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IGetPodcastHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastGetRequest podcastGetRequest,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);
}
