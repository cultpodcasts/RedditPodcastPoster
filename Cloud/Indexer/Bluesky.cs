using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Configuration;

namespace Indexer;

[DurableTask(nameof(Bluesky))]
public class Bluesky(
    IBlueskyPostManager blueskyPostManager,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<Bluesky> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var runStopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "{BlueskyName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Bluesky),
            context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogWarning("BlueskyCostProbe.Start instance-id='{InstanceId}'.", context.InstanceId);
        }

        if (!activityOptionsProvider.RunBluesky(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Bluesky), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Bluesky), reason);
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

            runStopwatch.Stop();
            if (_indexerOptions.EnableCostInstrumentation)
            {
                logger.LogWarning(
                    "BlueskyCostProbe.Complete instance-id='{InstanceId}' success='false' total-ms='{TotalMs}' error-type='{ErrorType}'.",
                    context.InstanceId,
                    runStopwatch.ElapsedMilliseconds,
                    ex.GetType().Name);
            }

            return indexerContext with { Success = false };
        }

        runStopwatch.Stop();
        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogWarning(
                "BlueskyCostProbe.Complete instance-id='{InstanceId}' success='true' total-ms='{TotalMs}'.",
                context.InstanceId,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}