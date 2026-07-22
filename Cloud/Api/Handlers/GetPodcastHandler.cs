using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class GetPodcastHandler(
    IPodcastGetService podcastGetService,
    ILogger<GetPodcastHandler> logger) : IGetPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastGetRequest podcastGetRequest,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await podcastGetService.GetAsync(podcastGetRequest, c);

        return result.Status switch
        {
            PodcastGetStatus.Found =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Podcast!, c),
            PodcastGetStatus.NotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c),
            PodcastGetStatus.Conflict when result.AmbiguousPodcasts != null =>
                await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(result.AmbiguousPodcasts, c),
            PodcastGetStatus.Conflict =>
                await req.CreateResponse(HttpStatusCode.Conflict)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c),
            PodcastGetStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Podcast get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve podcast"), c);
    }
}
