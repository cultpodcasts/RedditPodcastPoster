using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.DiscoverySchedule;

public interface IPutDiscoveryScheduleHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoveryScheduleUpdateRequest body,
        ClientPrincipal? cp,
        CancellationToken c);
}
