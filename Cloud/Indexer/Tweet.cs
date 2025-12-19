using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Twitter;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet(
    ITweeter tweeter,
    IActivityOptionsProvider activityOptionsProvider,
    ILogger<Tweet> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            "{TweetName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.", nameof(Tweet),
            context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

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
            return indexerContext with { Success = false };
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with { Success = true };
    }
}