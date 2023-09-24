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
    public async Task Run([TimerTrigger("0 */1 * * *"
#if DEBUG
            , RunOnStartup = false
#endif
        )] TimerInfo timerTimer
    )
    {
        _logger.LogInformation(
            $"{nameof(Indexer)}.{nameof(Run)} Initiated. Current timer schedule is: {timerTimer.ScheduleStatus.Next:R}");
        _logger.LogInformation(_indexerOptions.ToString());

        var indexOptions = _indexerOptions.ToIndexOptions();

        _logger.LogInformation(
            indexOptions.ReleasedSince.HasValue
                ? $"{nameof(Run)} Indexing with options released-since: '{indexOptions.ReleasedSince:dd/MM/yyyy HH:mm:ss}', bypass-youtube: '{indexOptions.SkipYouTubeUrlResolving}'."
                : $"{nameof(Run)} Indexing with options released-since: Null, bypass-youtube: '{indexOptions.SkipYouTubeUrlResolving}'.");

        await _podcastsUpdater.UpdatePodcasts(indexOptions);

        _logger.LogInformation(
            $"{nameof(Run)} Completed");
    }
}