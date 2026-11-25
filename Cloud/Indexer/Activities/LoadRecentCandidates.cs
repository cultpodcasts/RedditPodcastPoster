using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;

namespace Indexer.Activities;

public class LoadRecentCandidates(
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IOptions<PostingCriteria> postingCriteria,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<LoadRecentCandidates> logger)
{
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function(nameof(LoadRecentCandidates))]
    public async Task<IndexerContext> Run(
        [ActivityTrigger] IndexerContext indexerContext,
        FunctionContext functionContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(LoadRecentCandidates));

        logger.LogInformation(
            "{LoadRecentCandidatesName} initiated. task-activity-context-instance-id: '{ContextInstanceId}'.",
            nameof(LoadRecentCandidates), functionContext.InvocationId);
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
