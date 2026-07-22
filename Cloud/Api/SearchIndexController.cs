using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
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

public class SearchIndexController(
    IRunSearchIndexHandler runSearchIndexHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SearchIndexController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("SearchIndexRun")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "searchindex/run")]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct
    ) =>
        HandleRequest(
            req,
            ["admin"],
            runSearchIndexHandler.Handle,
            Unauthorised,
            ct);
}
