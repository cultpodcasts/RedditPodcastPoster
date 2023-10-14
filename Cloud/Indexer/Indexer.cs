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

        indexContext.SkipSpotifyUrlResolving = DateTime.UtcNow.Hour % 2 == 0;
        indexContext.SkipYouTubeUrlResolving = DateTime.UtcNow.Hour % 3 > 0;
        indexContext.SkipExpensiveQueries = DateTime.UtcNow.Hour % 12 > 0;

        _logger.LogInformation(
            indexContext.ReleasedSince.HasValue
                ? $"{nameof(RunAsync)} Indexing with options released-since: '{indexContext.ReleasedSince:dd/MM/yyyy HH:mm:ss}', bypass-spotify: '{indexContext.SkipSpotifyUrlResolving}', bypass-youtube: '{indexContext.SkipYouTubeUrlResolving}', bypass-expensive-queries: '{indexContext.SkipExpensiveQueries}'."
                : $"{nameof(RunAsync)} Indexing with options released-since: Null, bypass-spotify: '{indexContext.SkipSpotifyUrlResolving}', bypass-youtube: '{indexContext.SkipYouTubeUrlResolving}', bypass-expensive-queries: '{indexContext.SkipExpensiveQueries}'.");

        if (DryRun.IsDryRun)
        {
            return true;
        }

        bool results;
        try
        {
            results = await _podcastsUpdater.UpdatePodcasts(indexContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IPodcastsUpdater)}.{nameof(IPodcastsUpdater.UpdatePodcasts)}.");
            results = false;
        }

        if (!results)
        {
            _logger.LogError("Failure occurred");
        }

        _logger.LogInformation(
            $"{nameof(RunAsync)} Completed");
        return results;
    }
}