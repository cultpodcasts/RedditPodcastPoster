using System.Linq;
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

//[FunctionAuthorize]
public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
//    [Authorize(Policies.Submit)]
    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] [FromBody]
        SubmitUrlRequest request,
        HttpRequestData req)
    {
        logger.LogInformation("Headers: "+string.Join(",  ", req.Headers.Select(x => $"'{x.Key}':'{string.Join(", ", x.Value)}'")));
        var success = req.CreateResponse(HttpStatusCode.OK);
        await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("headers-test"));
        return success;

        //try
        //{
        //    logger.LogInformation(
        //        $"{nameof(Run)}: Handling url-submission: url: '{request.Url}', podcast-id: '{request.PodcastId}'.");
        //    await urlSubmitter.Submit(
        //        request.Url,
        //        new IndexingContext
        //        {
        //            SkipPodcastDiscovery = false,
        //            SkipExpensiveYouTubeQueries = false,
        //            SkipExpensiveSpotifyQueries = false
        //        },
        //        new SubmitOptions(request.PodcastId, true));
        //    var success = req.CreateResponse(HttpStatusCode.OK);
        //    await success.WriteAsJsonAsync(SubmitUrlResponse.Successful("success"));
        //    return success;
        //}
        //catch (Exception ex)
        //{
        //    logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{request.Url}'.");
        //}

        //var failure = req.CreateResponse(HttpStatusCode.BadRequest);
        //await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to accept"));
        //return failure;
    }
}