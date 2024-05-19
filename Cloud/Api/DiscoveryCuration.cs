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
        return HandleRequest(
            req,
            ["curate"],
            async (r, c) =>
            {
                var result = await discoveryResultsService.Get(c);
                return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(result, c);
            },
            (r, c) =>
                r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c),
            ct);
    }

    [Function("DiscoveryCurationPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] DiscoveryIngest discoveryIngest,
        CancellationToken ct)
    {
        return HandleRequest(
            req,
            ["curate"],
            discoveryIngest, async (r, m, c) =>
            {
                try
                {
                    var indexingContext = new IndexingContext
                    {
                        SkipPodcastDiscovery = false,
                        SkipExpensiveYouTubeQueries = false,
                        SkipExpensiveSpotifyQueries = false
                    };
                    var submitOptions = new SubmitOptions(null, true);
                    foreach (var url in m.Urls)
                    {
                        logger.LogInformation($"Submitting '{url}' with indexing-context: {indexingContext.ToString()}");
                        await urlSubmitter.Submit(url, indexingContext, submitOptions);
                    }

                    await discoveryResultsService.MarkAsProcessed(m.DiscoveryResultsDocumentIds);
                }
                catch (Exception e)
                {
                    logger.LogError(e, $"Failure handling post of {nameof(DiscoveryIngest)}");
                    return await r.CreateResponse(HttpStatusCode.InternalServerError).WithJsonBody(new { Message = "Success" }, c);

                }
                return await r.CreateResponse(HttpStatusCode.OK).WithJsonBody(new {Message = "Success"}, c);
            },
            (r, m, c) =>
                r.CreateResponse(HttpStatusCode.Unauthorized).WithJsonBody(new {Message = "Unauthorised"}, c),
            ct);
    }
}

