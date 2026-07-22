using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Models;
using Azure.Diagnostics;

namespace Api;

public class PublicController(
    IGetPublicEpisodeHandler getPublicEpisodeHandler,
    ILogger<EpisodeController> logger,
    IClientPrincipalFactory clientPrincipalFactory,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("PublicEpisodeGet")]
    public Task<HttpResponseData> GetByEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/episode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandlePublicRequest(
            req,
            new PodcastEpisodeRequestWrapper(episodeId),
            getPublicEpisodeHandler.Handle,
            Unauthorised,
            ct);
    }

    [Function("PublicPodcastEpisodeGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get",
            Route = "public/podcast/episode/{podcastId:guid}/{episodeId:guid}")]
        HttpRequestData req,
        String podcastName,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandlePublicRequest(
            req,
            new PodcastEpisodeRequestWrapper(podcastName, episodeId),
            getPublicEpisodeHandler.Handle,
            Unauthorised,
            ct);
    }
}
