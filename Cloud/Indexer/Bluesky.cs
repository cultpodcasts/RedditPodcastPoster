using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky;

namespace Indexer;

[DurableTask(nameof(Bluesky))]
public class Bluesky(
    IBlueskyPostManager blueskyPostManager,
    ILogger<Bluesky> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Bluesky)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());

        if (DryRun.IsBlueskyDryRun)
        {
            return indexerContext with {Success = true};
        }

        try
        {
            await blueskyPostManager.Post(
                indexerContext is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerContext is {SkipSpotifyUrlResolving: false, SpotifyError: false});
            logger.LogInformation("Bluesky-post executed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to execute {nameof(IBlueskyPostManager)}.{nameof(IBlueskyPostManager.Post)}.");
            return indexerContext with {Success = false};
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}