using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IPutDiscoveryScheduleHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoveryScheduleUpdateRequest body,
        ClientPrincipal? cp,
        CancellationToken c);
}
