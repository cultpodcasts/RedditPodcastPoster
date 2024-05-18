using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest submitUrlModel,
        CancellationToken ct
    )
    {
        return await req.HandleRequest(
            ["submit"],
            submitUrlModel, async (r, m, c) =>
            {
                try
                {
                    logger.LogInformation(
                        $"{nameof(Run)}: Handling url-submission: url: '{submitUrlModel.Url}', podcast-id: '{submitUrlModel.PodcastId}'.");
                    await urlSubmitter.Submit(
                        submitUrlModel.Url,
                        new IndexingContext
                        {
                            SkipPodcastDiscovery = false,
                            SkipExpensiveYouTubeQueries = false,
                            SkipExpensiveSpotifyQueries = false
                        },
                        new SubmitOptions(submitUrlModel.PodcastId, true));
                    var success = req.CreateResponse(HttpStatusCode.OK);
                    await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"), c);
                    return success;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{submitUrlModel.Url}'.");
                }

                var failure = req.CreateResponse(HttpStatusCode.BadRequest);
                await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to accept"), c);
                return failure;
            },
            async (r, m, c) =>
            {
                var failure = req.CreateResponse(HttpStatusCode.Forbidden);
                await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to accept"), c);
                return failure;
            },
            ct);
    }
}