using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Api.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class EpisodeController(
    IEpisodeHandler episodeHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<EpisodeController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    private const string? Route = "episode/{episodeId:guid}";

    [Function("EpisodeGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            episodeId, 
            episodeHandler.Get,
            Unauthorised, 
            ct);

    [Function("OutgoingEpisodesGet")]
    public Task<HttpResponseData> GetOutgoing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "episodes/outgoing")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            episodeHandler.GetOutgoing,
            Unauthorised,
            ct);

    [Function("EpisodePost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody] EpisodeChangeRequest episodeChangeRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            new EpisodeChangeRequestWrapper(episodeId, episodeChangeRequest), 
            episodeHandler.Post,
            Unauthorised, 
            ct);

    [Function("EpisodePublish")]
    public Task<HttpResponseData> EpisodePublish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "episode/publish/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody] EpisodePublishRequest episodePostRequest,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["curate"], 
            new EpisodePublishRequestWrapper(episodeId, episodePostRequest), 
            episodeHandler.Publish,
            Unauthorised,
            ct);

    [Function("EpisodeDelete")]
    public Task<HttpResponseData> EpisodeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "episode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["admin"], 
            episodeId, 
            episodeHandler.Delete, 
            Unauthorised, 
            ct);
}