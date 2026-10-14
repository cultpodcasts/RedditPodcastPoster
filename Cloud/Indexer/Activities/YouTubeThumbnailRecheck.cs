using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Azure.Diagnostics;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Thumbnails;

namespace Indexer.Activities;

/// <summary>
/// Re-checks recently released episodes whose stored YouTube image is <c>maxresdefault</c> / <c>hq720</c>
/// and replaces any YouTube has since withdrawn. Runs after the recent candidates load and before
/// posting, so social cards built later in the run use the replacement.
/// </summary>
[DurableTask(nameof(YouTubeThumbnailRecheck))]
public class YouTubeThumbnailRecheck(
    IRecentYouTubeThumbnailRechecker rechecker,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<YouTubeThumbnailRecheck> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var memoryProbe = memoryProbeOrchestrator.Start(nameof(YouTubeThumbnailRecheck));
        logger.LogInformation(
            "{YouTubeThumbnailRecheckName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.",
            nameof(YouTubeThumbnailRecheck), context.InstanceId);

        if (indexerContext.RecentEpisodeCandidates is not { Length: > 0 } candidates)
        {
            logger.LogInformation("{YouTubeThumbnailRecheckName}: no recent episode candidates.",
                nameof(YouTubeThumbnailRecheck));
            memoryProbe.End();
            return indexerContext;
        }

        try
        {
            await rechecker.Recheck(candidates, ToIndexingContext(indexerContext));
        }
        catch (Exception ex)
        {
            // Best effort: a failed re-check must not block posting or publishing.
            logger.LogError(ex, "{YouTubeThumbnailRecheckName} failed.", nameof(YouTubeThumbnailRecheck));
            memoryProbe.End(false, ex.GetType().Name);
            return indexerContext;
        }

        memoryProbe.End();
        return indexerContext;
    }

    /// <summary>
    /// Re-resolving needs a YouTube API call, so the re-check only runs when this run's index passes
    /// resolved YouTube without error.
    /// </summary>
    public static IndexingContext ToIndexingContext(IndexerContext indexerContext) =>
        new(SkipYouTubeUrlResolving:
            indexerContext.SkipYouTubeUrlResolving != false || indexerContext.YouTubeError == true);
}
