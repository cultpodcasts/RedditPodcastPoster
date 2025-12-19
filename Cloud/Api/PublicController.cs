using System.Net;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Api;

public class PublicController(ILogger<EpisodeController> logger,
    IPodcastRepository podcastRepository,
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PublicEpisodeGet")]
    public Task<HttpResponseData> Get(
    [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/episode/{episodeId:guid}")]
        HttpRequestData req,
    Guid episodeId,
    FunctionContext executionContext,
    CancellationToken ct
)
    {
        return HandlePublicRequest(req, episodeId, Get, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, Guid episodeId, ClientPrincipal? _,
    CancellationToken c)
    {
        try
        {
            logger.LogInformation("{GetName}: Get episode with id '{EpisodeId}'.", nameof(Get), episodeId);
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == episodeId));
            var episode = podcast?.Episodes.SingleOrDefault(x => x.Id == episodeId);

            if (episode == null || podcast == null || episode.Removed || podcast.IsRemoved())
            {
                logger.LogWarning("{GetName}: Episode with id '{EpisodeId}' not found.", nameof(Get), episodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var podcastEpisode = episode.EnrichPublic(podcast);
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(podcastEpisode, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to get episode.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c);
        return failure;
    }
}
