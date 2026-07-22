using System.Net;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Episodes;

public class PostEpisodeHandler(
    IEpisodeUpdateService episodeUpdateService,
    ILogger<PostEpisodeHandler> logger) : IPostEpisodeHandler
{
    public async Task<HttpResponseData> Handle(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c)
    {
        var result = await episodeUpdateService.UpdateAsync(episodeChangeRequestWrapper, c);

        return result.Status switch
        {
            EpisodeUpdateStatus.Accepted =>
                await req.CreateResponse(HttpStatusCode.Accepted)
                    .WithJsonBody(result.Outcome!.ToDto(), c),
            EpisodeUpdateStatus.NotFound =>
                req.CreateResponse(HttpStatusCode.NotFound),
            EpisodeUpdateStatus.Failed =>
                await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c),
            _ => await LogAndFail(req, c)
        };
    }

    private async Task<HttpResponseData> LogAndFail(HttpRequestData req, CancellationToken c)
    {
        logger.LogError("Episode update failed with unexpected status.");
        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update episode"), c);
    }
}
