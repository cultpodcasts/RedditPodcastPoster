using System.Net;
using Api.Auth;
using Api.Dtos;
using DarkLoop.Azure.Functions.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

[FunctionAuthorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
    [Authorize]
    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] [FromBody]
        SubmitUrlRequest request,
        HttpRequestData req)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Run)}: Handling url-submission: url: '{request.Url}', podcast-id: '{request.PodcastId}'.");
            await urlSubmitter.Submit(
                request.Url,
                new IndexingContext
                {
                    SkipPodcastDiscovery = false,
                    SkipExpensiveYouTubeQueries = false,
                    SkipExpensiveSpotifyQueries = false
                },
                new SubmitOptions(request.PodcastId, true));
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