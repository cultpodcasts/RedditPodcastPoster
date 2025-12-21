using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Podcast = Api.Dtos.Podcast;
using PodcastRenameRequest = Api.Models.PodcastRenameRequest;

namespace Api;

public class PodcastController(
    IPodcastHandler handler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PodcastController> logger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    [Function("PodcastRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/name/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        [FromBody] Dtos.PodcastRenameRequest newPodcastName,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["admin"],
            new PodcastRenameRequest(podcastName, newPodcastName.NewPodcastName), 
            handler.Rename,
            Unauthorised, 
            ct
        );

    [Function("PodcastIndex")]
    public Task<HttpResponseData> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/index/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            podcastName, 
            handler.Index, 
            Unauthorised, 
            ct);

    [Function("PodcastGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"],
            new PodcastGetRequest(podcastName, null), 
            handler.Get, 
            Unauthorised, 
            ct);

    [Function("PodcastGetWithEpisodeId")]
    public Task<HttpResponseData> GetWithEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "podcast/{podcastName}/{episodeId}")]
        HttpRequestData req,
        string podcastName,
        Guid episodeId,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["curate"],
            new PodcastGetRequest(podcastName, episodeId),
            handler.Get,
            Unauthorised,
            ct);


    [Function("PodcastPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        [FromBody] Podcast podcastChangeRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"],
            new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest),
            handler.Post,
            Unauthorised,
            ct);

    [Function("PodcastPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "podcast/{podcastId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        [FromBody] Podcast podcastChangeRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            new PodcastChangeRequestWrapper(podcastId, podcastChangeRequest, true),
            handler.Post, 
            Unauthorised, 
            ct);
}