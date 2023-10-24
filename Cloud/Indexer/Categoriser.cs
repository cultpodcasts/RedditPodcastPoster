using Indexer.Categorisation;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Indexer;

[DurableTask(nameof(Categoriser))]
public class Categoriser : TaskActivity<object, bool>
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

    public override async Task<bool> RunAsync(TaskActivityContext context, object input)
    {
        _logger.LogInformation($"{nameof(Categoriser)} initiated.");

        if (DryRun.IsDryRun)
        {
            return true;
        }

        try
        {
            await _recentEpisodeCategoriser.Categorise();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failure to execute {nameof(IRecentPodcastEpisodeCategoriser)}.{nameof(IRecentPodcastEpisodeCategoriser.Categorise)}.");
            return false;
        }

        _logger.LogInformation($"{nameof(RunAsync)} Completed");
        return true;
    }
}