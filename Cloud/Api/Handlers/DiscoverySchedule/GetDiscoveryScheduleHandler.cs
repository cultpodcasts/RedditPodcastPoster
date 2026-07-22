using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Extensions;
using Api.Models;
using Api.Services.DiscoverySchedule;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.DiscoverySchedule;

public class GetDiscoveryScheduleHandler(
    IDiscoveryScheduleGetService discoveryScheduleGetService,
    ILogger<GetDiscoveryScheduleHandler> logger) : IGetDiscoveryScheduleHandler
{
    private const int NextRunsPreviewCount = 6;

    public async Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c)
    {
        var result = await discoveryScheduleGetService.GetAsync(c);
        return result.Status switch
        {
            DiscoveryScheduleGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(
                    DiscoveryScheduleResponseBuilder.Build(
                        result.Config!,
                        result.IsDefault,
                        NextRunsPreviewCount),
                    c),
            DiscoveryScheduleGetStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Discovery schedule get failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
