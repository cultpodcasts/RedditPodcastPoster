using System.Net;
using Api.Dtos;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class DiscoveryCuration(
    IDiscoveryResultsService discoveryResultsService,
    IUrlSubmitter urlSubmitter,
    ILogger<DiscoveryCuration> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions)
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
        [FromBody] DiscoveryIngest discoveryIngest,
        CancellationToken ct)
    {
        return HandleRequest(req, ["curate"], discoveryIngest, Post, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Get(HttpRequestData r, CancellationToken c)
    {
        var result = await discoveryResultsService.Get(c);
        return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(result, c);
    }

    private async Task<HttpResponseData> Post(HttpRequestData r, DiscoveryIngest m, CancellationToken c)
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
            var submitResults = new List<(Guid DiscoveryItemId, string Message)>();

            foreach (var discoveryResult in discoveryResults)
            {
                logger.LogInformation(
                    $"Submitting discovery-result '{discoveryResult.Id}' with indexing-context: {indexingContext}");
                try
                {
                    var result = await urlSubmitter.Submit(discoveryResult, indexingContext, submitOptions);
                    submitResults.Add((discoveryResult.Id, result.ToString())!);
                }
                catch (Exception ex)
                {
                    errorsOccured = true;
                    submitResults.Add((discoveryResult.Id, "Error"));
                    logger.LogError(ex, $"{nameof(Post)} Failure submitting discovery-result '{discoveryResult.Id}'.");
                }
            }

            await discoveryResultsService.MarkAsProcessed(m.DiscoveryResultsDocumentIds);

            var response = new DiscoverySubmitResults
            {
                Message = "Success",
                ErrorsOccurred = errorsOccured,
                Results = submitResults
                    .Select(x => new DiscoveryItemResult
                    {
                        DiscoveryItemId = x.DiscoveryItemId,
                        Message = x.Message
                    })
                    .ToArray()
            };


            return await r.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(new {message = "Success", errorsOccurred = errorsOccured, results = submitResults}, c);
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failure handling post of {nameof(DiscoveryIngest)}");
        }

        return await r.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(new {Message = "Failure"}, c);
    }
}