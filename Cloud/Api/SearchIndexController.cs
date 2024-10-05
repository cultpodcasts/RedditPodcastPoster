using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Search;

namespace Api;

public class SearchIndexController(
    ISearchIndexerService searchIndexerService,
    ILogger<SearchIndexController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
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

    private async Task<HttpResponseData> RunIndexer(HttpRequestData req, CancellationToken c)
    {
        try
        {
            var result = await searchIndexerService.RunIndexer();
            return await req
                .CreateResponse(
                    result == IndexerState.Executed ? HttpStatusCode.OK : HttpStatusCode.BadRequest)
                .WithJsonBody(new {status = result.ToString()}, c);
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