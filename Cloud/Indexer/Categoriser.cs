using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Subjects;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser(
    IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
    IActivityOptionsProvider activityOptionsProvider,
    ILogger<Categoriser> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {interface}.{method)}.",
                nameof(IRecentPodcastEpisodeCategoriser), nameof(IRecentPodcastEpisodeCategoriser.Categorise));
            return indexerContext with { Success = false };
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}