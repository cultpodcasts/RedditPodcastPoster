using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Api.Configuration;
using Api;
using Api.Models;
using Api.Factories;
using Api.Handlers.TitleCasingRules;
using Azure.Diagnostics;

namespace Api.Controllers;

public class TitleCasingRulesController(
    IGetTitleCasingRulesHandler getTitleCasingRulesHandler,
    IGetTitleCasingRulesByLanguageHandler getTitleCasingRulesByLanguageHandler,
    IPutTitleCasingRulesHandler putTitleCasingRulesHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TitleCasingRulesController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string ListRoute = "title-casing-rules";
    private const string LanguageRoute = "title-casing-rules/{language}";

    [Function("TitleCasingRulesGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ListRoute)]
        HttpRequestData req,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            getTitleCasingRulesHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesGetByLanguage")]
    public Task<HttpResponseData> GetByLanguage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = LanguageRoute)]
        HttpRequestData req,
        string language,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            language,
            getTitleCasingRulesByLanguageHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = LanguageRoute)]
        HttpRequestData req,
        string language,
        FunctionContext _,
        [FromBody] LanguageTitleCasingRulesUpdateRequest body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["curate"],
            new TitleCasingRulesLanguageUpdate(language, body),
            putTitleCasingRulesHandler.Handle,
            Unauthorised,
            ct);
}
