using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos.Extensions;
using Api.Factories;
using Api.Handlers.News;
using Api.Handlers.Catalogue;
using Api.Models;
using Azure.Diagnostics;

namespace Api.Controllers;

public class NewsOrganisationController(
    IGetNewsOrganisationHandler getNewsOrganisationHandler,
    IPostNewsOrganisationHandler postNewsOrganisationHandler,
    ICatalogueRenameHandler catalogueRenameHandler,
    ICatalogueIndexHandler catalogueIndexHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<NewsOrganisationController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("NewsOrganisationGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "newsorganisation/{newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], newsOrganisationIdentifier, getNewsOrganisationHandler.Handle, Unauthorised, ct);

    // Rare fallback: App Service decodes %2F to '/', so names with '/' (e.g. "ANC 24/7") become
    // extra segments and miss NewsOrganisationGet (Azure/azure-functions-host#9290; same as PodcastGetSlash).
    [Function("NewsOrganisationGetSlash")]
    public Task<HttpResponseData> GetSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "newsorganisation/{*newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        CancellationToken ct) =>
        Get(req, newsOrganisationIdentifier, ct);

    [Function("NewsOrganisationPatch")]
    public Task<HttpResponseData> Patch(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "newsorganisation/{newsOrganisationId:guid}")]
        HttpRequestData req,
        Guid newsOrganisationId,
        [FromBody] Dtos.NewsOrganisationChangeRequest change,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new NewsOrganisationChangeRequestWrapper(newsOrganisationId, change.ToModel()),
            postNewsOrganisationHandler.Handle,
            Unauthorised,
            ct);

    /// <summary>Rename, like <c>PodcastRename</c>: admin only; records a redirect and propagates the new name.</summary>
    [Function("NewsOrganisationRename")]
    public Task<HttpResponseData> Rename(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsorganisation/name/{newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new CatalogueRenameCommand(newsOrganisationIdentifier, rename.NewName),
            catalogueRenameHandler.HandleNewsOrganisation,
            Unauthorised,
            ct);

    /// <summary>Rename when the old name contains <c>/</c> (App Service %2F decode; see NewsOrganisationGetSlash).</summary>
    [Function("NewsOrganisationRenameSlash")]
    public Task<HttpResponseData> RenameSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsorganisation/name/{*newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        [FromBody] Dtos.CatalogueRenameRequest rename,
        CancellationToken ct) =>
        Rename(req, newsOrganisationIdentifier, rename, ct);

    /// <summary>
    /// Index now, like <c>PodcastIndex</c>: curate only. The identifier is the guid or the (unique) name. Runs the hourly
    /// indexer's pass for this one parent and pushes what it saved to search.
    /// </summary>
    [Function("NewsOrganisationIndex")]
    public Task<HttpResponseData> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsorganisation/index/{newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], newsOrganisationIdentifier, catalogueIndexHandler.HandleNewsOrganisation, Unauthorised, ct);

    /// <summary>Index when the name contains <c>/</c> (App Service %2F decode; see NewsOrganisationGetSlash).</summary>
    [Function("NewsOrganisationIndexSlash")]
    public Task<HttpResponseData> IndexSlash(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "newsorganisation/index/{*newsOrganisationIdentifier}")]
        HttpRequestData req,
        string newsOrganisationIdentifier,
        CancellationToken ct) =>
        Index(req, newsOrganisationIdentifier, ct);
}
