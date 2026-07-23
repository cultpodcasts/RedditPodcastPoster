using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Models;
using Api.Factories;
using Api.Handlers.Terms;
using Azure.Diagnostics;

namespace Api.Controllers;

public class TermsController(
    IPostTermsHandler postTermsHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TermsController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "terms";

    [Function("TermPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] TermSubmitRequest termSubmitRequest,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            termSubmitRequest,
            postTermsHandler.Handle,
            Unauthorised,
            ct);
}
