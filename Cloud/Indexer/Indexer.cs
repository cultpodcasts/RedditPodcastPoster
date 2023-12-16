using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;

namespace Indexer;

[DurableTask(nameof(Indexer))]
public class Indexer : TaskActivity<IndexerContext, IndexerContext>
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

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation($"{nameof(Indexer)} initiated. Instance-id: '{context.InstanceId}', Indexer-Operation-Id: '{indexerContext.IndexerOperationId}'.");
        _logger.LogInformation(_indexerOptions.ToString());
        var indexingContext = _indexerOptions.ToIndexingContext();

        indexingContext.SkipSpotifyUrlResolving = false;
        indexingContext.SkipYouTubeUrlResolving = DateTime.UtcNow.Hour % 2 > 0;
        indexingContext.SkipExpensiveYouTubeQueries = DateTime.UtcNow.Hour % 12 > 0;
        indexingContext.SkipExpensiveSpotifyQueries = DateTime.UtcNow.Hour % 3 > 1;
        indexingContext.SkipPodcastDiscovery = true;

        var originalSkipYouTubeUrlResolving = indexingContext.SkipYouTubeUrlResolving;
        var originalSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving;

        _logger.LogInformation(
            indexingContext.ReleasedSince.HasValue
                ? $"{nameof(RunAsync)} Indexing with options released-since: '{indexingContext.ReleasedSince:dd/MM/yyyy HH:mm:ss}', bypass-spotify: '{indexingContext.SkipSpotifyUrlResolving}', bypass-youtube: '{indexingContext.SkipYouTubeUrlResolving}', bypass-expensive-spotify-queries: '{indexingContext.SkipExpensiveSpotifyQueries}', bypass-expensive-youtube-queries: '{indexingContext.SkipExpensiveYouTubeQueries}'."
                : $"{nameof(RunAsync)} Indexing with options released-since: Null, bypass-spotify: '{indexingContext.SkipSpotifyUrlResolving}', bypass-youtube: '{indexingContext.SkipYouTubeUrlResolving}', bypass-expensive-spotify-queries: '{indexingContext.SkipExpensiveSpotifyQueries}', bypass-expensive-youtube-queries: '{indexingContext.SkipExpensiveYouTubeQueries}'.");

        if (DryRun.IsDryRun)
        {
            return indexerContext.CompleteIndexerOperation(
                true, 
                indexingContext.SkipYouTubeUrlResolving,
                indexingContext.SkipYouTubeUrlResolving != originalSkipYouTubeUrlResolving,
                indexingContext.SkipSpotifyUrlResolving,
                indexingContext.SkipSpotifyUrlResolving != originalSkipSpotifyUrlResolving);
        }

        bool results;
        try
        {
            results = await _podcastsUpdater.UpdatePodcasts(indexingContext);
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
        return indexerContext.CompleteIndexerOperation(
            results, indexingContext.SkipYouTubeUrlResolving,
            indexingContext.SkipYouTubeUrlResolving != originalSkipYouTubeUrlResolving,
            indexingContext.SkipSpotifyUrlResolving,
            indexingContext.SkipSpotifyUrlResolving != originalSkipSpotifyUrlResolving);
    }
}