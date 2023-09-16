using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;

namespace Indexer;

public class Indexer
{
    private readonly IndexerOptions _indexerOptions;
    private readonly ILogger _logger;
    private readonly IPodcastsUpdater _podcastsUpdater;

    public Indexer(
        IPodcastsUpdater podcastsUpdater,
        IOptions<IndexerOptions> indexerOptions,
        ILogger<Indexer> logger)
    {
        _podcastsUpdater = podcastsUpdater;
        _indexerOptions = indexerOptions.Value;
        _logger = logger;
    }

    [Function("Indexer")]
    public async Task Run([TimerTrigger("0 */4 * * *")] TimerInfo timerTimer)
    {
        _logger.LogInformation($"{nameof(Run)} Initiated.");
        _logger.LogInformation(_indexerOptions.ToString());
        await _podcastsUpdater.UpdatePodcasts(_indexerOptions.ToIndexOptions());
        _logger.LogInformation(
            $"{nameof(Run)} Completed - Next timer schedule at: {timerTimer.ScheduleStatus.Next:dd/MM/yyyy HH:mm:ss}");
    }
}