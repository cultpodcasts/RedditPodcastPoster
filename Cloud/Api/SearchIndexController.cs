using Api.Configuration;
using Api.Handlers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api;

public class SearchIndexController(
    ISearchIndexHandler searchIndexHandler,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<SearchIndexController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
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
            searchIndexHandler.RunIndexer,
            Unauthorised,
            ct);
}