using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Subjects.Categorisation;

namespace Indexer.Activities;

[DurableTask(nameof(Categoriser))]
public class Categoriser(
    IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Categoriser> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Categoriser));

        logger.LogInformation(
            "{nameofCategoriser} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Categoriser), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (!activityOptionsProvider.RunCategoriser(out var reason))
        {
            logger.LogWarning("{class} activity disabled. Reason: '{reason}'.", nameof(Categoriser), reason);
            return indexerContext with { Success = true };
        }
        else
        {
            logger.LogInformation("{class} activity enabled. Reason: '{reason}'.", nameof(Categoriser), reason);
        }

        if (indexerContext.CategoriserOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.CategoriserOperationId));
        }

        try
        {
            await recentEpisodeCategoriser.Categorise(indexerContext.RecentEpisodeCandidates);
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        memoryProbe.End();

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}