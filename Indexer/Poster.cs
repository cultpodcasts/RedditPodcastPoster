using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;

namespace Poster;

public class Poster
{
    private readonly ILogger _logger;

    public Poster(
        ILogger<Poster> logger)
    {
        _logger = logger;
    }

    [Function("Poster")]
    public async Task Run([TimerTrigger("10 */4 * * *"
#if DEBUG
            , RunOnStartup = true
#endif
        )] TimerInfo timerTimer
    )
    {
        _logger.LogInformation(
            $"{nameof(Run)} Initiated. Current timer schedule is: {timerTimer.ScheduleStatus.Next:R}");

        _logger.LogInformation(
            $"{nameof(Run)} Completed");
    }
}