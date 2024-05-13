using System.Net;
using Api.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrl
{
    private readonly IUrlSubmitter _urlSubmitter;
    private readonly ILogger<SubmitUrl> _logger;

    public SubmitUrl(IUrlSubmitter urlSubmitter, ILogger<SubmitUrl> logger)
    {
        _urlSubmitter = urlSubmitter ?? throw new ArgumentNullException(nameof(urlSubmitter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("SubmitUrl")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] SubmitUrlRequest request
    )
    {
        try
        {
            _logger.LogInformation(
                $"{nameof(Run)}: Handling url-submission: url: '{request.Url}', podcast-id: '{request.PodcastId}'.");
            await _urlSubmitter.Submit(
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
            _logger.LogError(ex, $"{nameof(Run)}: Failed to submit url '{request.Url}'.");
        }

        var failure = req.CreateResponse(HttpStatusCode.BadRequest);
        await failure.WriteAsJsonAsync(SubmitUrlResponse.Failure("Unable to accept"));
        return failure;
    }
}