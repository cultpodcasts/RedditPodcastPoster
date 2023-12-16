using Indexer.Categorisation;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser : TaskActivity<IndexerContext, IndexerContext>
{
    private readonly ILogger<Categoriser> _logger;
    private readonly IRecentPodcastEpisodeCategoriser _recentEpisodeCategoriser;

    public Categoriser(
        IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
        ILogger<Categoriser> logger)
    {
        _recentEpisodeCategoriser = recentEpisodeCategoriser;
        _logger = logger;
    }

    public override async Task<IndexerContext> RunAsync(TaskActivityContext context, IndexerContext indexerContext)
    {
        _logger.LogInformation($"{nameof(Categoriser)} initiated. Instance-id: '{context.InstanceId}', Categoriser-Operation-Id: '{indexerContext.CategoriserOperationId}'.");

        if (DryRun.IsDryRun)
        {
            return indexerContext with {Success = true};
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

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerContext with {Success = true};
    }
}