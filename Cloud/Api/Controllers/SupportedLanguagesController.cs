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
    IPostSupportedLanguagesHandler postSupportedLanguagesHandler,
    IDeleteSupportedLanguagesHandler deleteSupportedLanguagesHandler,
    IGetNeutralCulturesHandler getNeutralCulturesHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SupportedLanguagesController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string? Route = "supported-languages";
    private const string? CulturesRoute = "supported-languages/cultures";
    private const string CodeRoute = "supported-languages/{code}";

    [Function("SupportedLanguagesGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            getSupportedLanguagesHandler.Handle,
            Unauthorised,
            ct);

    [Function("SupportedLanguagesCulturesGet")]
    public Task<HttpResponseData> GetCultures(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = CulturesRoute)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            getNeutralCulturesHandler.Handle,
            Unauthorised,
            ct);

    [Function("SupportedLanguagesPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext _,
        [FromBody] SupportedLanguageAddRequest body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            body,
            postSupportedLanguagesHandler.Handle,
            Unauthorised,
            ct);

    [Function("SupportedLanguagesDelete")]
    public Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = CodeRoute)]
        HttpRequestData req,
        string code,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            code,
            deleteSupportedLanguagesHandler.Handle,
            Unauthorised,
            ct);
}
