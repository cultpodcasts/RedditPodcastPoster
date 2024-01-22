using Azure;
using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet(
    ITweeter tweeter,
    IActivityMarshaller activityMarshaller,
    ILogger<Tweet> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Tweet)} initiated. Instance-id: '{context.InstanceId}', Tweeter-Operation-Id: '{indexerContext.TweetOperationId}'.");

        if (DryRun.IsTweetDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.TweetOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.TweetOperationId));
        }

        var activityBooked = await activityMarshaller.Initiate(indexerContext.TweetOperationId.Value, nameof(Tweet));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicateTweetOperation = true
            };
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
        finally
        {
            try
            {
                activityBooked =
                    await activityMarshaller.Complete(indexerContext.TweetOperationId.Value, nameof(Tweet));
                if (activityBooked != ActivityStatus.Completed)
                {
                    logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to complete activity.");
            }
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}