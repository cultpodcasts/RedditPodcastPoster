using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class CategorisedItemProcessor(
    IPodcastProcessor podcastProcessor,
    IPodcastRepository podcastRepository,
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
                await podcastRepository.Save(categorisedItem.MatchingPodcast);
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
                result.NewEpisode.Id);
            if (submitOptions.PersistToDatabase)
            {
                await podcastRepository.Save(result.NewPodcast);
            }
            else
            {
                logger.LogWarning("Bypassing persisting new-podcast.");
            }
        }

        return submitResult;
    }
}