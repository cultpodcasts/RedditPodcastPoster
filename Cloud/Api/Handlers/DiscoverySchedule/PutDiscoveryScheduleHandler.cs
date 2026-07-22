using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Extensions;
using Api.Models;
using Api.Services.DiscoverySchedule;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.DiscoverySchedule;

public class PutDiscoveryScheduleHandler(
    IDiscoveryScheduleUpdateService discoveryScheduleUpdateService,
    ILogger<PutDiscoveryScheduleHandler> logger) : IPutDiscoveryScheduleHandler
{
    private const int NextRunsPreviewCount = 6;

    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoveryScheduleUpdateRequest body,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await discoveryScheduleUpdateService.UpdateAsync(body, c);
        return result.Status switch
        {
            DiscoveryScheduleUpdateStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(
                    DiscoveryScheduleResponseBuilder.Build(
                        result.Config!,
                        isDefault: false,
                        NextRunsPreviewCount),
                    c),
            DiscoveryScheduleUpdateStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest)
                    .WithJsonBody(new { error = result.Error }, c),
            DiscoveryScheduleUpdateStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Discovery schedule update failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
