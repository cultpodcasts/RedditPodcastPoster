using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers.Catalogue;
using Azure.Diagnostics;

namespace Api.Controllers;

public class CatalogueIdentityController(
    ICatalogueIdentityLookupHandler lookupHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<CatalogueIdentityController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    /// <summary>
    /// Edit-time Identity tab lookup. Confident → ids; ambiguous → options (never auto-picked); no match → message.
    /// </summary>
    [Function("CatalogueIdentityLookup")]
    public Task<HttpResponseData> Lookup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "catalogue/identity/lookup")]
        HttpRequestData req,
        [FromBody] CatalogueIdentityLookupRequest body,
        CancellationToken ct) =>
        HandleRequest(req, ["curate"], body, lookupHandler.Handle, Unauthorised, ct);
}