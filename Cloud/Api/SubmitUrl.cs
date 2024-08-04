using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrl(
    IUrlSubmitter urlSubmitter,
    ISearchIndexerService searchIndexerService,
    ILogger<SubmitUrl> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions, baseLogger)
{
    [Function("SubmitUrl")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest submitUrlModel,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["submit"], submitUrlModel, Post, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Post(HttpRequestData req, SubmitUrlRequest submitUrlModel, CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Run)}: Handling url-submission: url: '{submitUrlModel.Url}', podcast-id: '{submitUrlModel.PodcastId}'.");
            var result = await urlSubmitter.Submit(
                submitUrlModel.Url,
                new IndexingContext
                {
                    SkipPodcastDiscovery = false,
                    SkipExpensiveYouTubeQueries = false,
                    SkipExpensiveSpotifyQueries = false
                },
                new SubmitOptions(submitUrlModel.PodcastId, true));

            await searchIndexerService.RunIndexer();

            var success = SubmitUrlResponse.Successful(result);
            var response = await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(success, c);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{submitUrlModel.Url}'.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Failure"), c);
        return failure;
    }
}