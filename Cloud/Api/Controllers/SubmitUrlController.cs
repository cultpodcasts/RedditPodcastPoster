using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Dtos;
using Api.Models;
using Api.Factories;
using Api.Handlers.SubmitUrl;
using Azure.Diagnostics;

namespace Api.Controllers;

public class SubmitUrlController(
    IPostSubmitUrlHandler postSubmitUrlHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SubmitUrlController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    [Function("SubmitUrl")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest submitUrlModel,
        CancellationToken ct
    ) => HandleRequest(
            req,
            ["submit"],
            submitUrlModel,
            postSubmitUrlHandler.Handle,
            Unauthorised,
            ct);
}
