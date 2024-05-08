using System.Net;
using DarkLoop.Azure.Functions.Authorization;
using Indexer.Auth;
using Indexer.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Indexer;

[FunctionAuthorize]
public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
    [Authorize(Policies.Submit)]
    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger("get", "post")] [FromBody]
        SubmitUrlRequest request,
        HttpRequestData req)
    {
        var indexOptions = new IndexingContext
        {
            SkipPodcastDiscovery = false,
            SkipExpensiveYouTubeQueries = false,
            SkipExpensiveSpotifyQueries = false
        };

        try
        {
            await urlSubmitter.Submit(
                request.Url,
                indexOptions,
                true,
                true,
                request.PodcastId,
                new SubmitOptions());
            var success = req.CreateResponse(HttpStatusCode.OK);
            await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"));
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{request.Url}'.");
        }

        var failure = req.CreateResponse(HttpStatusCode.BadRequest);
        await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to accept"));
        return failure;
    }
}