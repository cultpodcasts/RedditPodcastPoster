using System.Net;
using Api.Configuration;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Search;
using RedditPodcastPoster.UrlSubmission;

namespace Api;

public class SubmitUrlController(
    IPodcastRepository repository,
    IUrlSubmitter urlSubmitter,
    ISearchIndexerService searchIndexerService,
    ILogger<SubmitUrlController> logger,
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
                $"{nameof(Run)}: Handling url-submission: url: '{submitUrlModel.Url}', podcast-id: '{submitUrlModel.PodcastId}', podcast-name: '{submitUrlModel.PodcastName}'.");
            Guid? podcastId = null;
            if (!string.IsNullOrWhiteSpace(submitUrlModel.PodcastName))
            {
                var podcastIdWrapper =
                    await repository.GetBy(x => x.Name == submitUrlModel.PodcastName, x => new {guid = x.Id});
                if (podcastIdWrapper == null)
                {
                    return await req.CreateResponse(HttpStatusCode.NotFound)
                        .WithJsonBody(new {message = "Podcast with name not found"}, c);
                }

                podcastId = podcastIdWrapper.guid;
            }
            else
            {
                podcastId = submitUrlModel.PodcastId;
            }

            var submitOptions = new SubmitOptions(podcastId, true);
            var result = await urlSubmitter.Submit(
                submitUrlModel.Url,
                new IndexingContext
                {
                    SkipPodcastDiscovery = false,
                    SkipExpensiveYouTubeQueries = false,
                    SkipExpensiveSpotifyQueries = false
                },
                submitOptions);

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