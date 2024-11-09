using Azure;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Twitter;

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
            $"{nameof(Tweet)} initiated. task-activity-context-instance-id: '{context.InstanceId}'.");
        logger.LogInformation(indexerContext.ToString());

        if (DryRun.IsTweetDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.TweetOperationId == null)
        {
            logger.LogError(
                $"{nameof(Tweet)}.{nameof(RunAsync)}: Unable to track Tweet operation. {nameof(indexerContext)}.{nameof(indexerContext.TweetOperationId)} is null.");
            throw new ArgumentNullException(nameof(indexerContext.TweetOperationId));
        }

        logger.LogInformation($"{nameof(Tweet)}.{nameof(RunAsync)}: Marshall init.");

        var activityBooked = await activityMarshaller.Initiate(indexerContext.TweetOperationId.Value, nameof(Tweet));
        if (activityBooked != ActivityStatus.Initiated)
        {
            if (activityBooked == ActivityStatus.Failed)
            {
                return indexerContext with
                {
                    Success = false
                };
            }

            return indexerContext with
            {
                DuplicateTweetOperation = true
            };
        }

        logger.LogInformation($"{nameof(Tweet)}.{nameof(RunAsync)}: Marshall complete. Task is not duplicate.");

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