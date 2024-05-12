using Api.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
{
    private readonly IUrlSubmitter _urlSubmitter = urlSubmitter ?? throw new ArgumentNullException(nameof(urlSubmitter));
    private readonly ILogger<SubmitUrl> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    [Function("SubmitUrl")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        [Microsoft.Azure.Functions.Worker.Http.FromBody] SubmitUrlRequest request,
        HttpRequest req)
    {
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
        //    return new OkObjectResult(SubmitUrlResponse.Successful("success"));
        //}
        //catch (Exception ex)
        //{
        //    logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{request.Url}'.");
        //}

        return new BadRequestObjectResult(SubmitUrlResponse.Failure("Unable to accept"));
    }
}