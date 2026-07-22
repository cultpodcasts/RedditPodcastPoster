using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class IndexPodcastHandler(
    IPodcastIndexService podcastIndexService,
    ILogger<IndexPodcastHandler> logger) : IIndexPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        string podcastName,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await podcastIndexService.IndexAsync(podcastName, c);

        return result.Status switch
        {
            PodcastIndexStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Response!, c),
            PodcastIndexStatus.NotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound).WithJsonBody(result.Response!, c),
            PodcastIndexStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(result.Response!, c),
            PodcastIndexStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Podcast index failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c);
    }
}
