using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly ILogger<Tweet> _logger;
    private readonly ITweeter _tweeter;

    public Tweet(
        ITweeter tweeter,
        ILogger<Tweet> logger)
    {
        _tweeter = tweeter;
        _logger = logger;
    }

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation(
            $"{nameof(Tweet)} initiated. Instance-id: '{context.InstanceId}', Tweeter-Operation-Id: '{indexerContext.TweetOperationId}'.");

        if (DryRun.IsDryRun)
        {
            return indexerContext.WithSuccess(true);
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
            return indexerContext.WithSuccess(false);
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext.WithSuccess(true);
    }
}