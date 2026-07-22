using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class GetEpisodeHandler(
    IEpisodeGetService episodeGetService,
    ILogger<GetEpisodeHandler> logger) : IGetEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await episodeGetService.GetAsync(podcastEpisodeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeGetStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Episode!, c),
            EpisodeGetStatus.EpisodeNotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { message = "Episode not found." }, c),
            EpisodeGetStatus.PodcastNotFound =>
                await req.CreateResponse(HttpStatusCode.NotFound)
                    .WithJsonBody(new { message = "Podcast not found." }, c),
            EpisodeGetStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Episode get failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c);
    }
}
