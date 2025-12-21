using Api.Dtos;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface IDiscoveryCurationHandler
{
    Task<HttpResponseData> Get(HttpRequestData req, ClientPrincipal? cp, CancellationToken c);
    Task<HttpResponseData> Post(HttpRequestData req, DiscoverySubmitRequest model, ClientPrincipal? cp,
        CancellationToken c);
}