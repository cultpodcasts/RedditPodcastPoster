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
}
