using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Dtos;
using Api.Models;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Azure.Diagnostics;

namespace Api;

public class PushSubscriptionController(
    ICreatePushSubscriptionHandler createPushSubscriptionHandler,
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
            createPushSubscriptionHandler.Handle,
            Unauthorised,
            ct);
}
