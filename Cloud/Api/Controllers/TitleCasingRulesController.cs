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
    IGetTitleCasingRulesByLanguageHandler getTitleCasingRulesByLanguageHandler,
    IPostTitleCasingRulesLowerCaseTermHandler postTitleCasingRulesLowerCaseTermHandler,
    IDeleteTitleCasingRulesLowerCaseTermHandler deleteTitleCasingRulesLowerCaseTermHandler,
    IPostTitleCasingRulesKnownTermHandler postTitleCasingRulesKnownTermHandler,
    IDeleteTitleCasingRulesKnownTermHandler deleteTitleCasingRulesKnownTermHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<TitleCasingRulesController> logger,
    IOptions<HostingOptions> hostingOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator)
    : MemoryProbedHttpBaseClass(clientPrincipalFactory, hostingOptions, memoryProbeOrchestrator, logger)
{
    private const string LanguageRoute = "title-casing-rules/{language}";
    private const string LowerCaseTermsRoute = "title-casing-rules/{language}/lower-case-terms";
    private const string LowerCaseTermRoute = "title-casing-rules/{language}/lower-case-terms/{term}";
    private const string KnownTermsRoute = "title-casing-rules/{language}/known-terms";
    private const string KnownTermRoute = "title-casing-rules/{language}/known-terms/{literal}";

    [Function("TitleCasingRulesGetByLanguage")]
    public Task<HttpResponseData> GetByLanguage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = LanguageRoute)]
        HttpRequestData req,
        string language,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            language,
            getTitleCasingRulesByLanguageHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesPostLowerCaseTerm")]
    public Task<HttpResponseData> PostLowerCaseTerm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = LowerCaseTermsRoute)]
        HttpRequestData req,
        string language,
        FunctionContext _,
        [FromBody] TitleCasingRulesAddLowerCaseTermRequest body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new TitleCasingRulesLanguageTerm(language, body.Term),
            postTitleCasingRulesLowerCaseTermHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesDeleteLowerCaseTerm")]
    public Task<HttpResponseData> DeleteLowerCaseTerm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = LowerCaseTermRoute)]
        HttpRequestData req,
        string language,
        string term,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new TitleCasingRulesLanguageTerm(language, Uri.UnescapeDataString(term)),
            deleteTitleCasingRulesLowerCaseTermHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesPostKnownTerm")]
    public Task<HttpResponseData> PostKnownTerm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = KnownTermsRoute)]
        HttpRequestData req,
        string language,
        FunctionContext _,
        [FromBody] KnownTermUpdate body,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new TitleCasingRulesLanguageKnownTermAdd(language, body),
            postTitleCasingRulesKnownTermHandler.Handle,
            Unauthorised,
            ct);

    [Function("TitleCasingRulesDeleteKnownTerm")]
    public Task<HttpResponseData> DeleteKnownTerm(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = KnownTermRoute)]
        HttpRequestData req,
        string language,
        string literal,
        FunctionContext _,
        CancellationToken ct) =>
        HandleRequest(
            req,
            ["admin"],
            new TitleCasingRulesLanguageKnownTermDelete(language, Uri.UnescapeDataString(literal)),
            deleteTitleCasingRulesKnownTermHandler.Handle,
            Unauthorised,
            ct);
}
