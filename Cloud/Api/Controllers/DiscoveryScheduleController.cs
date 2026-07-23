using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Models;
using Api.Factories;
using Api.Handlers.DiscoverySchedule;
using Azure.Diagnostics;

namespace Api.Controllers;

public class DiscoveryScheduleController(
    IGetDiscoveryScheduleHandler getDiscoveryScheduleHandler,
    IPutDiscoveryScheduleHandler putDiscoveryScheduleHandler,
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
            getDiscoveryScheduleHandler.Handle,
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
            putDiscoveryScheduleHandler.Handle,
            Unauthorised,
            ct);
}
