using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Factories;
using Api.Handlers.TvShows;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class TvShowEpisodeController(
    IGetTvShowEpisodeHandler getTvShowEpisodeHandler,
    IPostTvShowEpisodeHandler postTvShowEpisodeHandler,
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

    [Function("TvShowEpisodePost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tvshowepisode/{episodeId:guid}")]
        HttpRequestData req,
        Guid episodeId,
        [FromBody] TvShowEpisodeChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new TvShowEpisodeChangeRequestWrapper(episodeId, change),
            postTvShowEpisodeHandler.Handle,
            Unauthorised,
            ct);
}
