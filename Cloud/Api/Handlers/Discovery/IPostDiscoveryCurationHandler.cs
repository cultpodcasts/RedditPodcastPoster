using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Discovery;

public interface IPostDiscoveryCurationHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoverySubmitRequest model,
        ClientPrincipal? cp,
        CancellationToken c);
}
