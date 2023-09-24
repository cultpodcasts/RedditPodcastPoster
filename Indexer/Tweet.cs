using Indexer.Tweets;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Indexer;

public class Tweet
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

    [Function("Tweet")]
    public async Task Run([TimerTrigger("6 */2 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo timerTimer
    )
    {
        _logger.LogInformation($"{nameof(Indexer)}.{nameof(Run)} Initiated.");

        await _tweeter.Tweet();

        _logger.LogInformation($"{nameof(Run)} Completed");
    }
}