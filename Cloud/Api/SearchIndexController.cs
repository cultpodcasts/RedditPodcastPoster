using System.Net;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Search;
using IndexerState = RedditPodcastPoster.Search.IndexerState;

namespace Api;

public class SearchIndexController(
    ISearchIndexerService searchIndexerService,
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
    )
    {
        return HandleRequest(req, ["admin"], RunIndexer, Unauthorised, ct);
    }

    private async Task<HttpResponseData> RunIndexer(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var result = await searchIndexerService.RunIndexer();
            return await req
                .CreateResponse(
                    result.IndexerState == IndexerState.Executed ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
                .WithJsonBody(result.ToDto(), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(RunIndexer)}: Failed to run indexer.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update podcast"), c);
        return failure;
    }
}