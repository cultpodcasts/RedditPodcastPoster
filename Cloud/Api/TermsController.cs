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

public class TermsController(
    ITermsHandler termsHandler,
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
            termsHandler.Post, 
            Unauthorised, 
            ct);
}