using System.Net;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.Search;
using IndexerState = RedditPodcastPoster.Search.IndexerState;

namespace Api.Handlers;

public class SearchIndexHandler(
    ISearchIndexerService searchIndexerService,
    ILogger<SearchIndexHandler>logger) : ISearchIndexHandler
{
    public async Task<HttpResponseData> RunIndexer(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
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