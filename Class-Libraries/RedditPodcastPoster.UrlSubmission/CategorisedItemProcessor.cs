using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class CategorisedItemProcessor(
    IPodcastProcessor podcastProcessor,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    IPodcastAndEpisodeFactory podcastAndEpisodeFactory,
    ILogger<CategorisedItem> logger) : ICategorisedItemProcessor
{
    public async Task<SubmitResult> ProcessCategorisedItem(CategorisedItem categorisedItem, SubmitOptions submitOptions)
    {
        void LogSubmitEpisodeState(SubmitResult submitResult)
        {
            if (submitResult.EpisodeResult is not (SubmitResultState.Created or SubmitResultState.Enriched))
            {
                return;
            }

            if (submitResult.Episode == null)
            {
                logger.LogError(
                    "ProcessCategorisedItem produced episode state '{EpisodeResult}' but no episode instance. Authority: '{Authority}', MatchingPodcastId: '{MatchingPodcastId}', PersistToDatabase: {PersistToDatabase}. Result: {SubmitResult}.",
                    submitResult.EpisodeResult,
                    categorisedItem.Authority,
                    categorisedItem.MatchingPodcast?.Id,
                    submitOptions.PersistToDatabase,
                    submitResult);
            }
            else
            {
                logger.LogInformation(
                    "ProcessCategorisedItem produced episode state '{EpisodeResult}' with episode id '{EpisodeId}'. Authority: '{Authority}', MatchingPodcastId: '{MatchingPodcastId}', PersistToDatabase: {PersistToDatabase}.",
                    submitResult.EpisodeResult,
                    submitResult.Episode.Id,
                    categorisedItem.Authority,
                    categorisedItem.MatchingPodcast?.Id,
                    submitOptions.PersistToDatabase);
            }
        }

        SubmitResult submitResult;
        if (categorisedItem.MatchingPodcast != null)
        {
            submitResult = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);

            if (submitOptions.PersistToDatabase)
            {
                if (submitResult is { PodcastResult: SubmitResultState.Enriched })
                {
                    await podcastRepository.Save(categorisedItem.MatchingPodcast);
                }

                if (submitResult is
                    { Episode: not null, EpisodeResult: SubmitResultState.Created or SubmitResultState.Enriched })
                {
                    await episodeRepository.Save(submitResult.Episode);
                }
            }
            else
            {
                logger.LogWarning("Bypassing persisting podcast.");
            }
        }
        else
        {
            var result = await podcastAndEpisodeFactory.CreatePodcastWithEpisode(categorisedItem);
            submitResult = new SubmitResult(SubmitResultState.Created,
                SubmitResultState.Created,
                result.SubmitEpisodeDetails,
                result.NewEpisode,
                result.NewPodcast);
            if (submitOptions.PersistToDatabase)
            {
                await podcastRepository.Save(result.NewPodcast);
                await episodeRepository.Save(result.NewEpisode);
            }
            else
            {
                logger.LogWarning("Bypassing persisting new-podcast.");
            }
        }

        LogSubmitEpisodeState(submitResult);
        return submitResult;
    }
}