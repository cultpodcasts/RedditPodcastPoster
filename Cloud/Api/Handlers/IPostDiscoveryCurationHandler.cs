using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPostDiscoveryCurationHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoverySubmitRequest model,
        ClientPrincipal? cp,
        CancellationToken c);
}
