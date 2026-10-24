using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.TvShows;
using Api.Handlers.Catalogue;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class TvShowController(
    IGetTvShowHandler getTvShowHandler,
    IPostTvShowHandler postTvShowHandler,
    ICatalogueRenameHandler catalogueRenameHandler,
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

    // Rare fallback: App Service decodes %2F to '/' (Azure/azure-functions-host#9290; same as PodcastGetSlash).
    [Function("TvShowGetSlash")]
    public Task<HttpResponseData> GetSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tvshow/{*tvShowIdentifier}")]
        HttpRequestData req,
        string tvShowIdentifier,
        CancellationToken ct) =>
        Get(req, tvShowIdentifier, ct);

    [Function("TvShowPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "tvshow/{tvShowId:guid}")]
        HttpRequestData req,
        Guid tvShowId,
        [FromBody] Dtos.TvShowChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new TvShowChangeRequestWrapper(tvShowId, change.ToModel()),
            postTvShowHandler.Handle,
            Unauthorised,
            ct);

    /// <summary>Rename, like <c>PodcastRename</c>: admin only; records a redirect and propagates the new name.</summary>
    [Function("TvShowRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tvshow/name/{tvShowIdentifier}")]
        HttpRequestData req,
        string tvShowIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new CatalogueRenameCommand(tvShowIdentifier, rename.NewName),
            catalogueRenameHandler.HandleTvShow,
            Unauthorised,
            ct);

    /// <summary>Rename when the old name contains <c>/</c> (App Service %2F decode; see TvShowGetSlash).</summary>
    [Function("TvShowRenameSlash")]
    public Task<HttpResponseData> RenameSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tvshow/name/{*tvShowIdentifier}")]
        HttpRequestData req,
        string tvShowIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        Rename(req, tvShowIdentifier, rename, ct);
}
