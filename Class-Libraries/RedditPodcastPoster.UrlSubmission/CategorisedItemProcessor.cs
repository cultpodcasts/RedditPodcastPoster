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
                result.NewEpisode);
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

        return submitResult;
    }
}