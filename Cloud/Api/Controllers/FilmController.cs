using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.Films;
using Api.Handlers.Catalogue;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class FilmController(
    IGetFilmHandler getFilmHandler,
    IPostFilmHandler postFilmHandler,
    ICatalogueRenameHandler catalogueRenameHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<FilmController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("FilmGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "film/{filmIdentifier}")]
        HttpRequestData req,
        string filmIdentifier,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], filmIdentifier, getFilmHandler.Handle, Unauthorised, ct);

    // Rare fallback: App Service decodes %2F to '/' (Azure/azure-functions-host#9290; same as PodcastGetSlash).
    [Function("FilmGetSlash")]
    public Task<HttpResponseData> GetSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "film/{*filmIdentifier}")]
        HttpRequestData req,
        string filmIdentifier,
        CancellationToken ct) =>
        Get(req, filmIdentifier, ct);

    [Function("FilmPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "film/{filmId:guid}")]
        HttpRequestData req,
        Guid filmId,
        [FromBody] Dtos.FilmChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new FilmChangeRequestWrapper(filmId, change.ToModel()),
            postFilmHandler.Handle,
            Unauthorised,
            ct);

    /// <summary>Rename, like <c>PodcastRename</c>: admin only; records a redirect and propagates the new name.</summary>
    [Function("FilmRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "film/name/{filmIdentifier}")]
        HttpRequestData req,
        string filmIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new CatalogueRenameCommand(filmIdentifier, rename.NewName),
            catalogueRenameHandler.HandleFilm,
            Unauthorised,
            ct);

    /// <summary>Rename when the old name contains <c>/</c> (App Service %2F decode; see FilmGetSlash).</summary>
    [Function("FilmRenameSlash")]
    public Task<HttpResponseData> RenameSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "film/name/{*filmIdentifier}")]
        HttpRequestData req,
        string filmIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        Rename(req, filmIdentifier, rename, ct);
}
