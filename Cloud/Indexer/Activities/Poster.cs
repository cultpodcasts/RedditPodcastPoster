using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Activities;

[DurableTask(nameof(Poster))]
public class Poster(
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<PosterOptions> posterOptions,
    IOptions<PostingCriteria> postingCriteria,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Poster> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly PosterOptions _posterOptions = posterOptions.Value;
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Poster));

        logger.LogInformation("{class} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Poster), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());
        logger.LogInformation(_posterOptions.ToString());
        logger.LogInformation(_postingCriteria.ToString());
        var baselineDate = DateTimeExtensions.DaysAgo(_posterOptions.ReleasedDaysAgo);

        logger.LogInformation(
            "{method} Posting with options released-since: '{baselineDate:O}', max-posts: '{posterOptionsMaxPosts}'.",
            nameof(RunAsync), baselineDate, _posterOptions.MaxPosts);

        if (!activityOptionsProvider.RunPoster(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Poster), reason);
            memoryProbe.End(true);
            return Task.FromResult(indexerContext with { Success = true });
        }

        logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Poster), reason);

        if (indexerContext.PosterOperationId == null)
        {
            memoryProbe.End(false, nameof(ArgumentNullException));
            throw new ArgumentNullException(nameof(indexerContext.PosterOperationId));
        }

        // Live Reddit.NET posting removed; RunPoster switch kept for a future Devvit poster.
        logger.LogInformation(
            "{method} Reddit posting is retired; skipping episode posts (released-since '{baselineDate:O}').",
            nameof(RunAsync),
            baselineDate);

        var result = indexerContext with { Success = true };
        memoryProbe.End(true);
        logger.LogInformation("{method} Completed. Result: {result}", nameof(RunAsync), result);
        return Task.FromResult(result);
    }
}
