using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public class PublishEpisodeHandler(
    IEpisodePublishService episodePublishService,
    ILogger<PublishEpisodeHandler> logger) : IPublishEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        EpisodePublishRequestWrapper publishRequest,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await episodePublishService.PublishAsync(publishRequest, c);

        return result.Status switch
        {
            EpisodePublishStatus.Ok =>
                await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(result.Response!, c),
            EpisodePublishStatus.BadRequest =>
                await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(result.Response!, c),
            EpisodePublishStatus.Failed =>
                req.CreateResponse(HttpStatusCode.InternalServerError),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Episode publish failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
