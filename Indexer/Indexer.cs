using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;

namespace Indexer;

public class Indexer
{
    private readonly IPodcastsUpdater _podcastsUpdater;
    private readonly ILogger _logger;

    public Indexer(IPodcastsUpdater podcastsUpdater, ILogger<Indexer> logger)
    {
        _podcastsUpdater = podcastsUpdater;
        _logger = logger;
    }

    [Function("Indexer")]
    public async Task Run([TimerTrigger("0 */4 * * *")] TimerInfo timerTimer)
    {
        _logger.LogInformation($"{nameof(Run)} - C# Timer trigger function executed at: {DateTime.Now}");
        _logger.LogInformation($"{nameof(Run)} - Next timer schedule at: {timerTimer.ScheduleStatus.Next}");
    }
}