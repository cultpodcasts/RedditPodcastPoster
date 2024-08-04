using System.Net;
using Api.Dtos;
using Indexer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Indexing;
using RedditPodcastPoster.Search;

namespace Api;

public class Podcast(
    IIndexer indexer,
    ISearchIndexerService searchIndexerService,
    ILogger<Podcast> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<IndexerOptions> indexerOptions,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    [Function("PodcastIndex")]
    public Task<HttpResponseData> Index(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "podcast/index/{podcastName}")]
        HttpRequestData req,
        string podcastName,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], podcastName, Index, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Index(HttpRequestData req, string podcastName, CancellationToken c)
    {
        try
        {
            logger.LogInformation($"{nameof(Index)} Index podcast '{podcastName}'.");

            if (_indexerOptions.ReleasedDaysAgo == null)
            {
                throw new InvalidOperationException("Unable to index with null released-days-ago.");
            }

            var indexingContext = _indexerOptions.ToIndexingContext() with
            {
                IndexSpotify = true,
                SkipSpotifyUrlResolving = false,
                SkipYouTubeUrlResolving = false,
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false,
                SkipPodcastDiscovery = true
            };

            await indexer.Index(podcastName, indexingContext);
            await searchIndexerService.RunIndexer();

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to index-podcast.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to index podcast"), c);
        return failure;
    }
}