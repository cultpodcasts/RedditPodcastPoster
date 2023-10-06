using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;

namespace Indexer;

[DurableTask(nameof(Indexer))]
public class Indexer : TaskActivity<object, bool>
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

    public override async Task<bool> RunAsync(TaskActivityContext context, object input)
    {
        _logger.LogInformation($"{nameof(Indexer)} initiated.");
        _logger.LogInformation(_indexerOptions.ToString());

        var indexContext = _indexerOptions.ToIndexOptions();

        _logger.LogInformation(
            indexContext.ReleasedSince.HasValue
                ? $"{nameof(RunAsync)} Indexing with options released-since: '{indexContext.ReleasedSince:dd/MM/yyyy HH:mm:ss}', bypass-youtube: '{indexContext.SkipYouTubeUrlResolving}'."
                : $"{nameof(RunAsync)} Indexing with options released-since: Null, bypass-youtube: '{indexContext.SkipYouTubeUrlResolving}'.");

        var results = await _podcastsUpdater.UpdatePodcasts(indexContext);
        if (results.Success)
        {
            _logger.LogInformation(results.ToString());
        }
        else
        {
            _logger.LogError(results.ToString());
        }

        _logger.LogInformation(
            $"{nameof(RunAsync)} Completed");
        return results.Success;
    }
}