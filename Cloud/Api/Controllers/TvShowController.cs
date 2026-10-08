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

public class TvShowController(
    IGetTvShowHandler getTvShowHandler,
    IPostTvShowHandler postTvShowHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TvShowController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("TvShowGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tvshow/{tvShowIdentifier}")]
        HttpRequestData req,
        string tvShowIdentifier,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], tvShowIdentifier, getTvShowHandler.Handle, Unauthorised, ct);

    [Function("TvShowPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "tvshow/{tvShowId:guid}")]
        HttpRequestData req,
        Guid tvShowId,
        [FromBody] TvShowChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new TvShowChangeRequestWrapper(tvShowId, change),
            postTvShowHandler.Handle,
            Unauthorised,
            ct);
}
