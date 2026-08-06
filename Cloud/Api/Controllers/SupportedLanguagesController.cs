using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Models;
using Api.Factories;
using Api.Handlers.SupportedLanguages;
using Azure.Diagnostics;

namespace Api.Controllers;

public class SupportedLanguagesController(
    IGetSupportedLanguagesHandler getSupportedLanguagesHandler,
    IPutSupportedLanguagesHandler putSupportedLanguagesHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SupportedLanguagesController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "supported-languages";

    [Function("SupportedLanguagesGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            getSupportedLanguagesHandler.Handle,
            Unauthorised,
            ct);

    [Function("SupportedLanguagesPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        [FromBody] SupportedLanguagesUpdateRequest body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            body,
            putSupportedLanguagesHandler.Handle,
            Unauthorised,
            ct);
}
