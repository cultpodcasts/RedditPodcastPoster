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

public class PushSubscriptionController(
    IPushSubscriptionHandler pushSubscriptionHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PushSubscriptionController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("PushSubscription")]
    public Task<HttpResponseData> CreatePushSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pushsubscription")]
        HttpRequestData req,
        [FromBody] PushSubscription pushSubscription,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req, 
            ["admin"], 
            pushSubscription, 
            pushSubscriptionHandler.CreatePushSubscription,
            Unauthorised, 
            ct);
}