using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Podcasts;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Podcasts;

public class PostPodcastHandler(
    IPodcastUpdateService podcastUpdateService,
    ILogger<PostPodcastHandler> logger) : IPostPodcastHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        ClientPrincipal? _,
        CancellationToken c)
    {
        var result = await podcastUpdateService.UpdateAsync(podcastChangeRequestWrapper, c);

        return result.Status switch
        {
            PodcastUpdateStatus.Accepted when result.FailureIndexingEpisodes =>
                await req.CreateResponse(HttpStatusCode.Accepted)
                    .WithJsonBody(new { failureIndexingEpisodes = true }, c),
            PodcastUpdateStatus.Accepted when result.FailureDeletingFromIndex =>
                await req.CreateResponse(HttpStatusCode.Accepted)
                    .WithJsonBody(new { failureDeletingFromIndex = true }, c),
            PodcastUpdateStatus.Accepted =>
                req.CreateResponse(HttpStatusCode.Accepted),
            PodcastUpdateStatus.NotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { id = result.PodcastId }, c),
            PodcastUpdateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(ApiErrorResponse.Failure("Unable to update podcast"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Podcast update failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(ApiErrorResponse.Failure("Unable to update podcast"), c);
    }
}
