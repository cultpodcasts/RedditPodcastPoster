using Indexer.Tweets;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Tweet))]
public class Tweet : TaskActivity<object, bool>
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

    public override async Task<bool> RunAsync(TaskActivityContext context, object input)
    {
        _logger.LogInformation($"{nameof(Tweet)} initiated.");

        if (DryRun.IsDryRun)
        {
            return true;
        }

        try
        {
            await _tweeter.Tweet();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failure to execute {nameof(ITweeter)}.{nameof(ITweeter.Tweet)}.");
            return false;
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return true;
    }
}