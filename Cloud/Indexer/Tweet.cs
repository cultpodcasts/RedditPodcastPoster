using System.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Twitter;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet(
    ITweeter tweeter,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    ILogger<Tweet> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var runStopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "{TweetName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Tweet),
            context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogWarning("TweetCostProbe.Start instance-id='{InstanceId}'.", context.InstanceId);
        }

        if (!activityOptionsProvider.RunTweet(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Tweet), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Tweet), reason);
        }

        try
        {
            await tweeter.Tweet(
                indexerContext is { SkipYouTubeUrlResolving: false, YouTubeError: false },
                indexerContext is { SkipSpotifyUrlResolving: false, SpotifyError: false });
            logger.LogInformation("Tweet executed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failure to execute {nameof(ITweeter)}.{nameof(ITweeter.Tweet)}.");

            runStopwatch.Stop();
            if (_indexerOptions.EnableCostInstrumentation)
            {
                logger.LogWarning(
                    "TweetCostProbe.Complete instance-id='{InstanceId}' success='false' total-ms='{TotalMs}' error-type='{ErrorType}'.",
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
                "TweetCostProbe.Complete instance-id='{InstanceId}' success='true' total-ms='{TotalMs}'.",
                context.InstanceId,
                runStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with { Success = true };
    }
}