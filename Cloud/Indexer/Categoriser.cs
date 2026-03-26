using System.Diagnostics;
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
    ILogger<Categoriser> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IndexerOptions _indexerOptions = indexerOptions.Value;

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        var runStopwatch = Stopwatch.StartNew();

        logger.LogInformation(
            "{nameofCategoriser} initiated. task-activity-context-instance-id: '{contextInstanceId}'.",
            nameof(Categoriser), context.InstanceId);
        logger.LogInformation(indexerContext.ToString());

        if (_indexerOptions.EnableCostInstrumentation)
        {
            logger.LogInformation(
                "CategoriserCostProbe.Start instance-id='{InstanceId}'.",
                context.InstanceId);
        }

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
            var categoriseStopwatch = Stopwatch.StartNew();
            await recentEpisodeCategoriser.Categorise();
            categoriseStopwatch.Stop();

            runStopwatch.Stop();
            if (_indexerOptions.EnableCostInstrumentation)
            {
                logger.LogInformation(
                    "CategoriserCostProbe.Complete instance-id='{InstanceId}' success='true' categorise-ms='{CategoriseMs}' total-ms='{TotalMs}'.",
                    context.InstanceId,
                    categoriseStopwatch.ElapsedMilliseconds,
                    runStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failure to execute {interface}.{method)}.",
                nameof(IRecentPodcastEpisodeCategoriser), nameof(IRecentPodcastEpisodeCategoriser.Categorise));

            runStopwatch.Stop();
            if (_indexerOptions.EnableCostInstrumentation)
            {
                logger.LogInformation(
                    "CategoriserCostProbe.Complete instance-id='{InstanceId}' success='false' total-ms='{TotalMs}' error-type='{ErrorType}'.",
                    context.InstanceId,
                    runStopwatch.ElapsedMilliseconds,
                    ex.GetType().Name);
            }

            return indexerContext with { Success = false };
        }

        logger.LogInformation("{method} Completed", nameof(RunAsync));
        return indexerContext with { Success = true };
    }
}