using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Api.Models;
using Azure.Diagnostics;
using Podcast = Api.Dtos.Podcast;
using PodcastRenameRequest = Api.Models.PodcastRenameRequest;

namespace Api;

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
            new PodcastRenameRequest(podcastName, newPodcastName.NewPodcastName),
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
    )
    {
        var podcastGetRequest = Guid.TryParse(podcastIdentifier, out var podcastId)
            ? new PodcastGetRequest(podcastId)
            : new PodcastGetRequest(podcastIdentifier, null);
        return HandleRequest(req, ["curate"], podcastGetRequest, getPodcastHandler.Handle, Unauthorised, ct);
    }

    [Function("PodcastGetWithEpisodeId")]
    public Task<HttpResponseData> GetWithEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastName}/{episodeId}")]
        HttpRequestData req,
        string podcastName,
        Guid episodeId,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            new PodcastGetRequest(podcastName, episodeId),
            getPodcastHandler.Handle,
            Unauthorised,
            ct);

    [Function("PodcastPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        [FromBody]
        Podcast podcastChangeRequest,
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
        [FromBody]
        Podcast podcastChangeRequest,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["curate"],
            new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest, true),
            postPodcastHandler.Handle,
            Unauthorised,
            ct);
}
