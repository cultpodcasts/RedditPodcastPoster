using Azure.Diagnostics;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Subjects;

namespace Indexer;

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
            await recentEpisodeCategoriser.Categorise();
            memoryProbe.End();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {interface}.{method)}.",
                nameof(IRecentPodcastEpisodeCategoriser), nameof(IRecentPodcastEpisodeCategoriser.Categorise));

            memoryProbe.End(false, ex.GetType().Name);

            return indexerContext with { Success = false };
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}