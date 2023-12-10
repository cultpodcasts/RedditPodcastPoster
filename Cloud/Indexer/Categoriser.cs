using Indexer.Categorisation;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser : TaskActivity<IndexerResponse, IndexerResponse>
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

    public override async Task<IndexerResponse> RunAsync(TaskActivityContext context, IndexerResponse indexerResponse)
    {
        _logger.LogInformation($"{nameof(Categoriser)} initiated. Instance-id: '{context.InstanceId}'.");

        if (DryRun.IsDryRun)
        {
            return indexerResponse with {Success = true};
        }

        try
        {
            await _recentEpisodeCategoriser.Categorise();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IRecentPodcastEpisodeCategoriser)}.{nameof(IRecentPodcastEpisodeCategoriser.Categorise)}.");
            return indexerResponse with {Success = false};
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return indexerResponse with {Success = true};
    }
}