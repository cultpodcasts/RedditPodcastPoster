using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.Public;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Public;

public class GetPublicEpisodeHandler(
    IPublicEpisodeGetService publicEpisodeGetService,
    ILogger<GetPublicEpisodeHandler> logger) : IGetPublicEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await publicEpisodeGetService.GetAsync(podcastEpisodeRequestWrapper, c);
        return result.Status switch
        {
            PublicEpisodeGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK)
                    .WithJsonBody(result.Episode!.ToDto(result.Podcast!), c),
            PublicEpisodeGetStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            PublicEpisodeGetStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(ApiErrorResponse.Failure("Unable to retrieve episode"), c),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Public episode get failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
