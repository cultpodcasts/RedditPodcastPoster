using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos;
using Api.Models;
using Api.Factories;
using Api.Handlers.Discovery;
using Azure.Diagnostics;

namespace Api.Controllers;

public class DiscoveryCurationController(
    IGetDiscoveryCurationHandler getDiscoveryCurationHandler,
    IPostDiscoveryCurationHandler postDiscoveryCurationHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<DiscoveryCurationController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "DiscoveryCuration";

    [Function("DiscoveryCurationGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            getDiscoveryCurationHandler.Handle,
            Unauthorised,
            ct);

    [Function("DiscoveryCurationPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        [FromBody] DiscoverySubmitRequest discoverySubmitRequest,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            discoverySubmitRequest,
            postDiscoveryCurationHandler.Handle,
            Unauthorised,
            ct);
}
