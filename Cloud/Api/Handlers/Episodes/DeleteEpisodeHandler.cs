using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Episodes;

public class DeleteEpisodeHandler(
    IEpisodeDeleteService episodeDeleteService,
    ILogger<DeleteEpisodeHandler> logger) : IDeleteEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await episodeDeleteService.DeleteAsync(podcastEpisodeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeDeleteStatus.Deleted =>
                req.CreateResponse(HttpStatusCode.OK),
            EpisodeDeleteStatus.PodcastConflict =>
                req.CreateResponse(HttpStatusCode.Conflict),
            EpisodeDeleteStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            EpisodeDeleteStatus.AlreadySocial =>
                await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(
                    new
                    {
                        message = "Cannot remove episode.",
                        posted = result.Posted,
                        tweeted = result.Tweeted
                    }, c),
            _ => LogAndFail(req)
        };
    }

    private HttpResponseData LogAndFail(HttpRequestData req)
    {
        logger.LogError("Episode delete failed with unexpected status.");
        return req.CreateResponse(HttpStatusCode.InternalServerError);
    }
}
