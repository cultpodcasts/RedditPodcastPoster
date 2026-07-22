using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Discovery;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PostDiscoveryCurationHandler(
    IDiscoveryCurationSubmitService discoveryCurationSubmitService,
    ILogger<PostDiscoveryCurationHandler> logger) : IPostDiscoveryCurationHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        DiscoverySubmitRequest model,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await discoveryCurationSubmitService.SubmitAsync(model, c);

        return result.Status switch
        {
            DiscoveryCurationSubmitStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Response!, c),
            DiscoveryCurationSubmitStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(new { Message = "Failure" }, c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Discovery curation submit failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(new { Message = "Failure" }, c);
    }
}
