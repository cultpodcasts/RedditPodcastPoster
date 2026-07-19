using Api.Configuration;
using Api.Dtos;
using Api.Factories;
using Api.Handlers;
using Azure.Diagnostics;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class DiscoveryScheduleController(
    IDiscoveryScheduleHandler discoveryScheduleHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<DiscoveryScheduleController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "discovery-schedule";

    [Function("DiscoveryScheduleGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            discoveryScheduleHandler.Get,
            Unauthorised,
            ct);

    [Function("DiscoverySchedulePut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        [FromBody] DiscoveryScheduleUpdateRequest body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            body,
            discoveryScheduleHandler.Put,
            Unauthorised,
            ct);
}
