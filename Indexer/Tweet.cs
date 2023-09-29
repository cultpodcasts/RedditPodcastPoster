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
            , RunOnStartup = false
#endif
        )]
        TimerInfo timerTimer
    )
    {
        return;
        _logger.LogInformation($"{nameof(Tweet)}.{nameof(Run)} Initiated. Current timer schedule is: {timerTimer.ScheduleStatus.Next:R}");

        await _tweeter.Tweet();

        _logger.LogInformation($"{nameof(Run)} Completed");
    }
}