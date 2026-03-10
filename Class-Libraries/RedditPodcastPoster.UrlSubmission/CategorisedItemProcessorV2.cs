using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 implementation that processes categorised items using detached repositories.
/// </summary>
public class CategorisedItemProcessorV2(
    IPodcastProcessorV2 podcastProcessor,
    IPodcastRepositoryV2 podcastRepository,
    IPodcastAndEpisodeFactoryV2 podcastAndEpisodeFactory,
    ILogger<CategorisedItemProcessorV2> logger) : ICategorisedItemProcessorV2
{
    public async Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, 
        SubmitOptions submitOptions)
    {
        SubmitResult submitResult;
        
        if (categorisedItem.MatchingPodcast != null)
        {
            // Add episode to existing podcast
            submitResult = await podcastProcessor.AddEpisodeToExistingPodcast(categorisedItem);

            if (!submitOptions.PersistToDatabase)
            {
                logger.LogWarning("Bypassing persisting podcast updates.");
            }
            // Note: PodcastProcessorV2 handles persistence internally
        }
        else
        {
            // Create new podcast with episode
            var result = await podcastAndEpisodeFactory.CreatePodcastWithEpisode(categorisedItem);
            
            submitResult = new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.Created,
                result.SubmitEpisodeDetails,
                result.NewEpisode);

            if (!submitOptions.PersistToDatabase)
            {
                logger.LogWarning("Bypassing persisting new-podcast.");
            }
            // Note: PodcastAndEpisodeFactoryV2 handles persistence internally
        }

        return submitResult;
    }
}
