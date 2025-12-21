using System.Net;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Handlers;

public class SubmitUrlHandler(
    IPodcastRepository repository,
    IUrlSubmitter urlSubmitter,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<SubmitUrlHandler> logger) : ISubmitUrlHandler
{
    public async Task<HttpResponseData> Post(HttpRequestData req, SubmitUrlRequest submitUrlModel, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                "{RunName}: Handling url-submission: url: '{Url}', podcast-id: '{PodcastId}', podcast-name: '{PodcastName}'.",
                nameof(Post), submitUrlModel.Url, submitUrlModel.PodcastId, submitUrlModel.PodcastName);
            Guid? podcastId;
            if (!string.IsNullOrWhiteSpace(submitUrlModel.PodcastName))
            {
                var podcastIdWrapper =
                    await repository.GetBy(x => x.Name == submitUrlModel.PodcastName, x => new { guid = x.Id });
                if (podcastIdWrapper == null)
                {
                    return await req.CreateResponse(HttpStatusCode.NotFound)
                        .WithJsonBody(new { message = "Podcast with name not found" }, c);
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

            if (result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                try
                {
                    await episodeSearchIndexerService.IndexEpisode(result.EpisodeId!.Value, c);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to index episode after submission. EpisodeId: '{EpisodeId}'.",
                        result.EpisodeId);
                }
            }

            var success = SubmitUrlResponse.Successful(result);
            var response = await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(success, c);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RunName}: Failed to submit url '{Url}'.", nameof(Post), submitUrlModel.Url);
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Failure"), c);
        return failure;
    }
}