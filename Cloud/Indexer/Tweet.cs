using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet(
    ITweeter tweeter,
    ILogger<Tweet> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Tweet)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());

        if (DryRun.IsTweetDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.TweetOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.TweetOperationId));
        }

        try
        {
            await tweeter.Tweet(
                indexerContext is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerContext is {SkipSpotifyUrlResolving: false, SpotifyError: false});
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failure to execute {nameof(ITweeter)}.{nameof(ITweeter.Tweet)}.");
            return indexerContext with {Success = false};
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}