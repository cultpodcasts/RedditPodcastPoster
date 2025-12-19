using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky;

namespace Indexer;

[DurableTask(nameof(Bluesky))]
public class Bluesky(
    IBlueskyPostManager blueskyPostManager,
    IActivityOptionsProvider activityOptionsProvider,
    ILogger<Bluesky> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            "{BlueskyName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Bluesky),
            context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunBluesky())
        {
            logger.LogInformation("{class} activity disabled.", nameof(Bluesky));
            return indexerContext with { Success = true };
        }

        try
        {
            await blueskyPostManager.Post(
                indexerContext is { SkipYouTubeUrlResolving: false, YouTubeError: false },
                indexerContext is { SkipSpotifyUrlResolving: false, SpotifyError: false });
            logger.LogInformation("Bluesky-post executed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to execute {object}.{method)}.",
                nameof(IBlueskyPostManager), nameof(IBlueskyPostManager.Post));
            return indexerContext with { Success = false };
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}