using Azure;
using Indexer.Categorisation;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly IActivityMarshaller _activityMarshaller;
    private readonly ILogger<Categoriser> _logger;
    private readonly IRecentPodcastEpisodeCategoriser _recentEpisodeCategoriser;

    public Categoriser(
        IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
        IActivityMarshaller activityMarshaller,
        ILogger<Categoriser> logger)
    {
        _recentEpisodeCategoriser = recentEpisodeCategoriser;
        _activityMarshaller = activityMarshaller;
        _logger = logger;
    }

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation(
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
            await _activityMarshaller.Initiate(indexerContext.CategoriserOperationId.Value, nameof(Categoriser));
        if (activityBooked != ActivityStatus.Initiated)
        {
            return indexerContext with
            {
                DuplicateCategoriserOperation = true
            };
        }


        try
        {
            await _recentEpisodeCategoriser.Categorise();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IRecentPodcastEpisodeCategoriser)}.{nameof(IRecentPodcastEpisodeCategoriser.Categorise)}.");
            return indexerContext with {Success = false};
        }
        finally
        {
            try
            {
                activityBooked = await _activityMarshaller.Complete(indexerContext.CategoriserOperationId.Value,
                    nameof(Categoriser));
                if (activityBooked != ActivityStatus.Completed)
                {
                    _logger.LogError("Failure to complete activity");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failure to complete activity.");
            }
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}