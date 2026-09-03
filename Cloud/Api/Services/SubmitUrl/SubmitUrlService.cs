using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Services.SubmitUrl;

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
            Guid? podcastId = submitUrlModel.PodcastId;
            // Name-only: unique → attach by id; many → 409; none → leave id unset so ingest creates a series with PodcastName.
            if (podcastId == null && !string.IsNullOrWhiteSpace(submitUrlModel.PodcastName))
            {
                var matches = await PodcastNameAttachLookup.FindByName(
                    repository,
                    submitUrlModel.PodcastName,
                    cancellationToken);

                if (matches.Count > 1)
                {
                    logger.LogWarning(
                        "{RunName}: Podcast name '{PodcastName}' matches {Count} rows; refusing first-iterator attach. Ids: {Ids}.",
                        nameof(SubmitAsync),
                        submitUrlModel.PodcastName,
                        matches.Count,
                        string.Join(", ", matches.Select(x => x.Id)));
                    return new SubmitUrlResult(
                        SubmitUrlStatus.Conflict,
                        AmbiguousPodcasts: matches.Select(x => x.Id));
                }

                if (matches.Count == 1)
                {
                    podcastId = matches[0].Id;
                }
            }

            var submitOptions = new SubmitOptions(podcastId, true, PodcastName: submitUrlModel.PodcastName);
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

            return new SubmitUrlResult(SubmitUrlStatus.Ok, result);
        }
        catch (AmbiguousPodcastNameException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Ambiguous podcast name '{PodcastName}' on submit of '{Url}'.",
                nameof(SubmitAsync),
                ex.PodcastName,
                submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.Conflict, AmbiguousPodcasts: ex.PodcastIds);
        }
        catch (SubmitPodcastNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "{RunName}: Submit referenced missing podcast id '{PodcastId}' for '{Url}'.",
                nameof(SubmitAsync),
                ex.PodcastId,
                submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.PodcastNotFound, Message: "Podcast not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RunName}: Failed to submit url '{Url}'.", nameof(SubmitAsync), submitUrlModel.Url);
            return new SubmitUrlResult(SubmitUrlStatus.Failed, Message: "Failure");
        }
    }
}
