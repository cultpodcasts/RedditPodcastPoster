using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

using Indexer.Models;
using Indexer.Services;

namespace Indexer.Activities;

[DurableTask(nameof(LoadRecentCandidates))]
public class LoadRecentCandidates(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IOptions<PostingCriteria> postingCriteria,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<LoadRecentCandidates> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(LoadRecentCandidates));

        logger.LogInformation(
            "{LoadRecentCandidatesName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.",
            nameof(LoadRecentCandidates), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (indexerContext.RecentEpisodeCandidates != null)
        {
            logger.LogInformation(
                "Recent episode candidates already preloaded. Count: {Count}.",
                indexerContext.RecentEpisodeCandidates.Length);
            memoryProbe.End();
            return indexerContext;
        }

        var releasedSince = DateTimeExtensions.DaysAgo(postingCriteria.Value.MaxDays);
        var candidates = await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(releasedSince);

        logger.LogInformation(
            "Loaded {Count} recent active episode candidates since '{ReleasedSince:O}'.",
            candidates.Count,
            releasedSince);

        memoryProbe.End();
        return indexerContext with { RecentEpisodeCandidates = candidates.ToArray() };
    }
}
