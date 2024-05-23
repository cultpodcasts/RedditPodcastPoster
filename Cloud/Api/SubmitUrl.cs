using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrl(
    IUrlSubmitter urlSubmitter,
    ILogger<SubmitUrl> logger,
    IOptions<HostingOptions> hostingOptions)
    : BaseHttpFunction(hostingOptions)
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

            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(SubmitUrlResponse.Successful(result), c);
            return success;
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