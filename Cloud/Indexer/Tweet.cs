using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet : TaskActivity<IndexerResponse, IndexerResponse>
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

    public override async Task<IndexerResponse> RunAsync(TaskActivityContext context, IndexerResponse indexerResponse)
    {
        _logger.LogInformation($"{nameof(Tweet)} initiated.");

        if (DryRun.IsDryRun)
        {
            return indexerResponse with {Success = true};
        }

        try
        {
            await _tweeter.Tweet(
                indexerResponse is {SkipYouTubeUrlResolving: false, YouTubeError: false},
                indexerResponse is {SkipSpotifyUrlResolving: false, SpotifyError: false});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failure to execute {nameof(ITweeter)}.{nameof(ITweeter.Tweet)}.");
            return indexerResponse with {Success = false};
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerResponse with {Success = true};
    }
}