using Api.Dtos;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;

namespace Api.Services.SubmitUrl;

public interface ISubmitUrlService
{
    Task<SubmitUrlResult> SubmitAsync(SubmitUrlRequest submitUrlModel, CancellationToken cancellationToken);
}

public class SubmitUrlService(
    IPodcastRepository repository,
    IUrlSubmitter urlSubmitter,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<SubmitUrlService> logger) : ISubmitUrlService
{
    public async Task<SubmitUrlResult> SubmitAsync(
        SubmitUrlRequest submitUrlModel,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation(
                "{RunName}: Handling url-submission: url: '{Url}', podcast-id: '{PodcastId}', podcast-name: '{PodcastName}'.",
                nameof(SubmitAsync), submitUrlModel.Url, submitUrlModel.PodcastId, submitUrlModel.PodcastName);
            Guid? podcastId;
            if (!string.IsNullOrWhiteSpace(submitUrlModel.PodcastName))
            {
                var podcast = await repository.GetBy(x => x.Name == submitUrlModel.PodcastName);
                if (podcast == null)
                {
                    return new SubmitUrlResult(
                        SubmitUrlStatus.PodcastNotFound,
                        Message: "Podcast with name not found");
                }

                podcastId = podcast.Id;
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

            var episodeId = result.Episode?.Id;
            if (result.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                if (episodeId.HasValue)
                {
                    try
                    {
                        await episodeSearchIndexerService.IndexEpisode(episodeId.Value, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to index episode after submission. EpisodeId: '{EpisodeId}'.",
                            episodeId.Value);
                    }
                }
                else
                {
                    logger.LogError(
                        "Submit result indicated episode state '{EpisodeResult}' but no episode id was returned. Url: '{Url}'.",
                        result.EpisodeResult,
                        submitUrlModel.Url);
                }
            }

            return new SubmitUrlResult(SubmitUrlStatus.Ok, SubmitUrlResponse.Successful(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RunName}: Failed to submit url '{Url}'.", nameof(SubmitAsync), submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.Failed, SubmitUrlResponse.Failure("Failure"));
        }
    }
}
