using Azure;
using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IActivityMarshaller _activityMarshaller;
    private readonly ILogger<Tweet> _logger;
    private readonly ITweeter _tweeter;

    public Tweet(
        ITweeter tweeter,
        IActivityMarshaller activityMarshaller,
        ILogger<Tweet> logger)
    {
        _tweeter = tweeter;
        _activityMarshaller = activityMarshaller;
        _logger = logger;
    }

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation(
            $"{nameof(Tweet)} initiated. Instance-id: '{context.InstanceId}', Tweeter-Operation-Id: '{indexerContext.TweetOperationId}'.");

        if (DryRun.IsTweetDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.TweetOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.TweetOperationId));
        }

        var activityBooked = await _activityMarshaller.Initiate(indexerContext.TweetOperationId.Value, nameof(Tweet));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicateTweetOperation = true
            };
        }

        try
        {
            await _tweeter.Tweet(
                indexerContext is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerContext is {SkipSpotifyUrlResolving: false, SpotifyError: false});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failure to execute {nameof(ITweeter)}.{nameof(ITweeter.Tweet)}.");
            return indexerContext with {Success = false};
        }
        finally
        {
            try
            {
                activityBooked =
                    await _activityMarshaller.Complete(indexerContext.TweetOperationId.Value, nameof(Tweet));
                if (activityBooked != ActivityStatus.Completed)
                {
                    _logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to complete activity.");
            }
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}