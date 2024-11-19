using System.Net;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class DiscoveryCurationController(
    IDiscoveryResultsService discoveryResultsService,
    IUrlSubmitter urlSubmitter,
    ISearchIndexerService searchIndexerService,
    IClientPrincipalFactory clientPrincipalFactory,
    ILogger<DiscoveryCurationController> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(clientPrincipalFactory, hostingOptions, logger)
{
    private const string? Route = "DiscoveryCuration";

    [Function("DiscoveryCurationGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)]
        HttpRequestData req,
        FunctionContext executionContext,
        CancellationToken ct)
    {
        return HandleRequest(req, ["curate"], Get, Unauthorised, ct);
    }

    [Function("DiscoveryCurationPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] DiscoverySubmitRequest discoverySubmitRequest,
        CancellationToken ct)
    {
        return HandleRequest(req, ["curate"], discoverySubmitRequest, Post, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Get(HttpRequestData r, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var result = await discoveryResultsService.Get(c);
            return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(result, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to obtain discovery-results.");
            return r.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    private async Task<HttpResponseData> Post(HttpRequestData r, DiscoverySubmitRequest m, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            var errorsOccured = false;
            var indexingContext = new IndexingContext
            {
                SkipPodcastDiscovery = false,
                SkipExpensiveYouTubeQueries = false,
                SkipExpensiveSpotifyQueries = false
            };
            var submitOptions = new SubmitOptions(null, true);

            var discoveryResults = await discoveryResultsService.GetDiscoveryResult(m);
            var submitResults = new List<DiscoverySubmitResponseItem>();
            var erroredResults = new List<Guid>();

            foreach (var discoveryResult in discoveryResults)
            {
                logger.LogInformation(
                    $"Submitting discovery-result '{discoveryResult.Id}' with indexing-context: {indexingContext}");
                try
                {
                    var result = await urlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                    submitResults.Add(
                        new DiscoverySubmitResponseItem
                        {
                            DiscoveryItemId = discoveryResult.Id,
                            EpisodeId = result.EpisodeId,
                            Message = result.State.ToString()
                        });
                }
                catch (Exception ex)
                {
                    erroredResults.Add(discoveryResult.Id);
                    errorsOccured = true;
                    submitResults.Add(
                        new DiscoverySubmitResponseItem
                        {
                            DiscoveryItemId = discoveryResult.Id,
                            Message = "Error"
                        });
                    logger.LogError(ex, $"{nameof(Post)} Failure submitting discovery-result '{discoveryResult.Id}'.");
                }
            }

            await discoveryResultsService.MarkAsProcessed(m.DiscoveryResultsDocumentIds, m.ResultIds,
                erroredResults.ToArray());

            await searchIndexerService.RunIndexer();

            var response = new DiscoverySubmitResponse
            {
                Message = "Success",
                ErrorsOccurred = errorsOccured,
                Results = [.. submitResults]
            };

            return await r
                .CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(response, c);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failure handling post of {nameof(DiscoverySubmitRequest)}");
        }

        return await r.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(new {Message = "Failure"}, c);
    }
}