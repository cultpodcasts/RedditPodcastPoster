using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos;
using Api.Factories;
using Api.Handlers.Podcasts;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class PodcastController(
    IGetPodcastHandler getPodcastHandler,
    IPostPodcastHandler postPodcastHandler,
    IIndexPodcastHandler indexPodcastHandler,
    IRenamePodcastHandler renamePodcastHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PodcastController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator
) : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("PodcastRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/name/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        [FromBody]
        Dtos.PodcastRenameRequest newPodcastName,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["admin"],
            new PodcastRenameCommand(podcastName, newPodcastName.NewPodcastName),
            renamePodcastHandler.Handle,
            Unauthorised,
            ct
        );

    [Function("PodcastIndex")]
    public Task<HttpResponseData> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/index/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            PodcastRouteNameNormalizer.Normalize(podcastName),
            indexPodcastHandler.Handle,
            Unauthorised,
            ct);

    [Function("PodcastGet")]
    public Task<HttpResponseData> GetByIdentifier(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastIdentifier}")]
        HttpRequestData req,
        string podcastIdentifier,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            PodcastGetRequest.FromRouteIdentifier(podcastIdentifier),
            getPodcastHandler.Handle,
            Unauthorised,
            ct);

    [Function("PodcastGetWithEpisodeId")]
    public Task<HttpResponseData> GetWithEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastName}/{episodeId:guid}")]
        HttpRequestData req,
        string podcastName,
        Guid episodeId,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            PodcastGetRequest.FromRouteIdentifier(podcastName, episodeId),
            getPodcastHandler.Handle,
            Unauthorised,
            ct);

    // Rare fallback: App Service decodes %2F to '/', so names with '/' become
    // extra segments and miss the routes above (Azure/azure-functions-host#9290).
    // Also stops Guid-bind 500s when a split name wrongly hits PodcastGetWithEpisodeId.
    [Function("PodcastGetSlash")]
    public Task<HttpResponseData> GetByIdentifierCatchAll(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{*podcastIdentifier}")]
        HttpRequestData req,
        string podcastIdentifier,
        CancellationToken ct
    )
    {
        if (PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
                podcastIdentifier, out var podcastName, out var episodeId))
        {
            return GetWithEpisodeId(req, podcastName, episodeId, ct);
        }

        return GetByIdentifier(req, podcastIdentifier, ct);
    }

    [Function("PodcastPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        [FromBody] PodcastChangeRequest podcastChangeRequest,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest),
            postPodcastHandler.Handle,
            Unauthorised,
            ct);

    [Function("PodcastPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        [FromBody] PodcastChangeRequest podcastChangeRequest,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest, true),
            postPodcastHandler.Handle,
            Unauthorised,
            ct);
}
