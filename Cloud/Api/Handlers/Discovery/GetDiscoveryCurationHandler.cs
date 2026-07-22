using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos.Extensions;
using Api.Dtos.Mapping;
using Api.Extensions;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Discovery;

public class GetDiscoveryCurationHandler(
    IDiscoveryCurationGetService discoveryCurationGetService,
    ILogger<GetDiscoveryCurationHandler> logger) : IGetDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Handle(HttpRequestData req, ClientPrincipal? cp, CancellationToken c)
    {
        var includeHidden = bool.TryParse(req.Query["includeHidden"], out var parsed) && parsed;
        var result = await discoveryCurationGetService.GetAsync(includeHidden, c);

        return result.Status switch
        {
            DiscoveryCurationGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Data!.ToDto(), c),
            DiscoveryCurationGetStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Discovery curation get failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
