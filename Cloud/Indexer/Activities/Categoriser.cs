using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Diagnostics;
using Indexer.Models;
using Indexer.Services;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Subjects.Categorisation;

namespace Indexer.Activities;

public class Categoriser(
    IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
    IRecentCataloguePlayableCategoriser recentCataloguePlayableCategoriser,
    IActivityOptionsProvider activityOptionsProvider,
    IOptions<IndexerOptions> indexerOptions,
    IMemoryProbeOrchestrator memoryProbeOrchestrator,
    ILogger<Categoriser> logger)
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;
    private readonly IMemoryProbeOrchestrator _memoryProbeOrchestrator = memoryProbeOrchestrator;

    [Function(nameof(Categoriser))]
    public async Task<IndexerContext> Run(
        [ActivityTrigger] IndexerContext indexerContext,
        FunctionContext functionContext)
    {
        var memoryProbe = _memoryProbeOrchestrator.Start(nameof(Categoriser));

        logger.LogInformation(
            "{nameofCategoriser} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Categoriser), functionContext.InvocationId);
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
            var cataloguePlayables = await recentCataloguePlayableCategoriser.Categorise();
            logger.LogInformation(
                "{class} catalogue playables categorised. Count: {count}.",
                nameof(Categoriser),
                cataloguePlayables.Count);
        }
        catch (Exception ex)
        {
            memoryProbe.End(false, ex.GetType().Name);
            throw;
        }

        memoryProbe.End();

        logger.LogInformation("{method} Completed", nameof(Run));
        return indexerContext with { Success = true };
    }
}
