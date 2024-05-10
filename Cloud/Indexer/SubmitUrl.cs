using System.Net;
using System.Text.Json;
using DarkLoop.Azure.Functions.Authorization;
using Indexer.Auth;
using Indexer.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.UrlSubmission;

namespace Indexer;

[FunctionAuthorize]
public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
    [Authorize(Policies.Submit)]
    [Function("SubmitUrl")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] [FromBody]
        SubmitUrlRequest request,
        HttpRequestData req)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Run)}: Handling url-submission: url: '{request.Url}', podcast-id: '{request.PodcastId}'.");
            var task= urlSubmitter.Submit(
                request.Url,
                new IndexingContext
                {
                    SkipPodcastDiscovery = false,
                    SkipExpensiveYouTubeQueries = false,
                    SkipExpensiveSpotifyQueries = false
                },
                new SubmitOptions(request.PodcastId, true));
            task.GetAwaiter().GetResult();
            var success = req.CreateResponse(HttpStatusCode.OK);
            success.WriteString(JsonSerializer.Serialize(SubmitUrlResponse.Successful("success")));
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{request.Url}'.");
        }

        var failure = req.CreateResponse(HttpStatusCode.BadRequest);
        failure.WriteString(JsonSerializer.Serialize(SubmitUrlResponse.Failure("Unable to accept")));
        return failure;
    }
}