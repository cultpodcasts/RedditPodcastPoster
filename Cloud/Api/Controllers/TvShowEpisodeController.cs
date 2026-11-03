using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.Episodes;
using Api.Handlers.TvShows;
using Api.Models;
using Azure.Diagnostics;
using RedditPodcastPoster.Models.ContentKinds;

namespace Api.Controllers;

public class TvShowEpisodeController(
    IGetTvShowEpisodeHandler getTvShowEpisodeHandler,
    IPostTvShowEpisodeHandler postTvShowEpisodeHandler,
    IPublishEpisodeHandler publishEpisodeHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TvShowEpisodeController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("TvShowEpisodeGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tvshowepisode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], episodeId, getTvShowEpisodeHandler.Handle, Unauthorised, ct);

    [Function("TvShowEpisodePatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "tvshowepisode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        [FromBody] Dtos.TvShowEpisodeChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new TvShowEpisodeChangeRequestWrapper(episodeId, change.ToModel()),
            postTvShowEpisodeHandler.Handle,
            Unauthorised,
            ct);

    [Function("TvShowEpisodePublish")]
    public Task<HttpResponseData> Publish(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tvshowepisode/{episodeId:guid}/publish")]
        HttpRequestData req,
        Guid episodeId,
        [FromBody] Dtos.EpisodePublishRequest episodePostRequest,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new EpisodePublishRequestWrapper(
                null, episodeId, episodePostRequest.ToModel(), ContentKind.TvShowEpisode),
            publishEpisodeHandler.Handle,
            Unauthorised,
            ct);
}
