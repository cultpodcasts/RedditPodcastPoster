using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Api.Models;
using Azure.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class EpisodeController(
    IEpisodeHandler episodeHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<EpisodeController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "episode/{episodeId:guid}";
    private const string? PodcastIdentifierRoute = "episode/{podcastIdentifier}/{episodeId:guid}";
    private const string? PodcastIdRoute = "episode/{podcastId:guid}/{episodeId:guid}";


    [Function("PodcastNameEpisodeGet")]
    public Task<HttpResponseData> GetByPodcastIdentifier(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = PodcastIdentifierRoute)]
        HttpRequestData req,
        string podcastIdentifier,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        var podcastEpisodeGetRequest = Guid.TryParse(podcastIdentifier, out var podcastId)
            ? new PodcastEpisodeRequestWrapper(podcastId, episodeId)
            : new PodcastEpisodeRequestWrapper(podcastIdentifier, episodeId);
        return HandleRequest(
            req,
            ["curate"],
            podcastEpisodeGetRequest,
            episodeHandler.Get,
            Unauthorised,
            ct);
    }


    [Function("EpisodeGet")]
    public Task<HttpResponseData> GetByEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        logger.LogWarning("{method}: Fetching episode by ID: {EpisodeId} without any podcast-identifier",
            nameof(GetByEpisodeId), episodeId);
        return HandleRequest(
            req,
            ["curate"],
            new PodcastEpisodeRequestWrapper(episodeId),
            episodeHandler.Get,
            Unauthorised,
            ct);
    }

    [Function("OutgoingEpisodesGet")]
    public Task<HttpResponseData> GetOutgoing(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "episodes/outgoing")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(
            req,
            ["curate"],
            episodeHandler.GetOutgoing,
            Unauthorised,
            ct);
    }

    [Function("EpisodePost")]
    public Task<HttpResponseData> PostEpisodeByEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody]
        EpisodeChangeRequest episodeChangeRequest,
        CancellationToken ct
    )
    {
        logger.LogWarning("{method}: Fetching episode by ID: {EpisodeId} without any podcast-identifier",
            nameof(PostEpisodeByEpisodeId), episodeId);
        return HandleRequest(
            req,
            ["curate"],
            new EpisodeChangeRequestWrapper(null, episodeId, episodeChangeRequest),
            episodeHandler.Post,
            Unauthorised,
            ct);
    }

    [Function("PodcastEpisodePost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = PodcastIdRoute)]
        HttpRequestData req,
        Guid podcastId,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody]
        EpisodeChangeRequest episodeChangeRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(
            req,
            ["curate"],
            new EpisodeChangeRequestWrapper(podcastId, episodeId, episodeChangeRequest),
            episodeHandler.Post,
            Unauthorised,
            ct);
    }

    [Function("EpisodePublish")]
    public Task<HttpResponseData> PublishEpisodeByEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "episode/publish/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody]
        EpisodePublishRequest episodePostRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(
            req,
            ["curate"],
            new EpisodePublishRequestWrapper(null, episodeId, episodePostRequest),
            episodeHandler.Publish,
            Unauthorised,
            ct);
    }

    [Function("PodcastEpisodePublish")]
    public Task<HttpResponseData> Publish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "episode/publish/{podcastId:guid}/{episodeId:guid}")]
        HttpRequestData req,
        Guid podcastId,
        Guid episodeId,
        FunctionContext executionContext,
        [FromBody]
        EpisodePublishRequest episodePostRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(
            req,
            ["curate"],
            new EpisodePublishRequestWrapper(podcastId, episodeId, episodePostRequest),
            episodeHandler.Publish,
            Unauthorised,
            ct);
    }

    [Function("EpisodeDelete")]
    public Task<HttpResponseData> DeleteEpisodeByEpisodeId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = Route)]
        HttpRequestData req,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        logger.LogWarning("{method}: Deleting episode by ID: {EpisodeId} without any podcast-identifier",
            nameof(DeleteEpisodeByEpisodeId), episodeId);
        return HandleRequest(
            req,
            ["admin"],
            new PodcastEpisodeRequestWrapper(episodeId),
            episodeHandler.Delete,
            Unauthorised,
            ct);
    }

    [Function("PodcastEpisodeDelete")]
    public Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = PodcastIdRoute)]
        HttpRequestData req,
        Guid podcastId,
        Guid episodeId,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(
            req,
            ["admin"],
            new PodcastEpisodeRequestWrapper(podcastId, episodeId),
            episodeHandler.Delete,
            Unauthorised,
            ct);
    }
}