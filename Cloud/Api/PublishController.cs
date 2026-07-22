using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api.Factories;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Azure.Diagnostics;

namespace Api;

public class PublishController(
    IPublishHomepageHandler publishHomepageHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<PublishController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("PublishHomepage")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "publish/homepage")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["admin"],
            publishHomepageHandler.Handle,
            Unauthorised,
            ct);
}
