using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Submitters;

public class UrlSubmitter(
    IPodcastRepository podcastRepository,
    IPodcastService podcastService,
    IUrlCategoriser urlCategoriser,
    ICategorisedItemProcessor categorisedItemProcessor,
    ILogger<UrlSubmitter> logger)
    : IUrlSubmitter
{
    public async Task<SubmitResult> Submit(
        Uri url,
        IndexingContext indexingContext,
        SubmitOptions submitOptions)
    {
        var episodeResult = SubmitResultState.None;
        try
        {
            Podcast? podcast = null;
            if (!submitOptions.CreatePodcast)
            {
                if (submitOptions.PodcastId != null)
                {
                    podcast = await podcastRepository.GetPodcast(submitOptions.PodcastId.Value);
                    if (podcast == null)
                    {
                        logger.LogWarning(
                            "Submit referenced podcast id '{PodcastId}' which does not exist.",
                            submitOptions.PodcastId.Value);
                        throw new SubmitPodcastNotFoundException(submitOptions.PodcastId.Value);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(submitOptions.PodcastName))
                {
                    var matches = await PodcastNameAttachLookup.FindByName(
                        podcastRepository,
                        submitOptions.PodcastName);
                    if (matches.Count > 1)
                    {
                        logger.LogWarning(
                            "Podcast name '{PodcastName}' matches {Count} rows; refusing first-iterator attach. Ids: {Ids}.",
                            submitOptions.PodcastName,
                            matches.Count,
                            string.Join(", ", matches.Select(x => x.Id)));
                        throw new AmbiguousPodcastNameException(
                            submitOptions.PodcastName,
                            matches.Select(x => x.Id).ToArray());
                    }

                    podcast = matches.Count == 1 ? matches[0] : null;
                }
                else
                {
                    podcast = await podcastService.GetPodcastFromEpisodeUrl(url, indexingContext);
                }

                if (podcast != null && podcast.IsRemoved())
                {
                    logger.LogWarning("Podcast with id '{podcastId}' is removed.", podcast.Id);
                    return new SubmitResult(episodeResult, SubmitResultState.PodcastRemoved);
                }
            }

            var categorisedItem =
                await urlCategoriser.Categorise(
                    podcast,
                    url,
                    indexingContext,
                    submitOptions.MatchOtherServices,
                    submitOptions.PrefetchedMeta);

            var submitResult = await categorisedItemProcessor.ProcessCategorisedItem(categorisedItem, submitOptions);

            if (submitResult.EpisodeResult is SubmitResultState.Created or SubmitResultState.Enriched)
            {
                if (submitResult.Episode == null)
                {
                    logger.LogError(
                        "Submit completed with episode state '{EpisodeResult}' but no episode instance. Url: '{Url}', PodcastId: '{PodcastId}', CreatePodcast: {CreatePodcast}. Result: {SubmitResult}.",
                        submitResult.EpisodeResult,
                        url,
                        submitOptions.PodcastId,
                        submitOptions.CreatePodcast,
                        submitResult);
                }
                else
                {
                    logger.LogInformation(
                        "Submit completed with episode state '{EpisodeResult}' and episode id '{EpisodeId}'. Url: '{Url}'.",
                        submitResult.EpisodeResult,
                        submitResult.Episode.Id,
                        url);
                }
            }

            return submitResult;
        }
        catch (AmbiguousPodcastNameException)
        {
            throw;
        }
        catch (SubmitPodcastNotFoundException)
        {
            throw;
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Error ingesting '{url}'. Http-request-exception with status: '{status}'", url,
                e.StatusCode);
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error ingesting '{url}'.", url);
            return new SubmitResult(SubmitResultState.None, SubmitResultState.None);
        }
    }
}
