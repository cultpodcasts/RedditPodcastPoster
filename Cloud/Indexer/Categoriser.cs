using Azure;
using Indexer.Categorisation;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser(
    IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
    IActivityMarshaller activityMarshaller,
    ILogger<Categoriser> logger)
    : TaskActivity<IndexerContext, IndexerContext>
{
    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        logger.LogInformation(
            $"{nameof(Categoriser)} initiated. Instance-id: '{context.InstanceId}', Categoriser-Operation-Id: '{indexerContext.CategoriserOperationId}'.");

        if (DryRun.IsCategoriserDryRun)
        {
            return indexerContext with {Success = true};
        }

        if (indexerContext.CategoriserOperationId == null)
        {
            throw new ArgumentNullException(nameof(indexerContext.CategoriserOperationId));
        }

        var activityBooked =
            await activityMarshaller.Initiate(indexerContext.CategoriserOperationId.Value, nameof(Categoriser));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicateCategoriserOperation = true
            };
        }


        try
        {
            await recentEpisodeCategoriser.Categorise();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                $"Failure to execute {nameof(IRecentPodcastEpisodeCategoriser)}.{nameof(IRecentPodcastEpisodeCategoriser.Categorise)}.");
            return indexerContext with {Success = false};
        }
        finally
        {
            try
            {
                activityBooked = await activityMarshaller.Complete(indexerContext.CategoriserOperationId.Value,
                    nameof(Categoriser));
                if (activityBooked != ActivityStatus.Completed)
                {
                    logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to complete activity.");
            }
        }

        logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}