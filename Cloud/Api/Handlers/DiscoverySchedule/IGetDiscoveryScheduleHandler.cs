using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.DiscoverySchedule;

public interface IGetDiscoveryScheduleHandler
{
    Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c);
}
