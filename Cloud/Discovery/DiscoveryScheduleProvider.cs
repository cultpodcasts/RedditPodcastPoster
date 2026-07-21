using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Discovery;

public interface IDiscoveryScheduleProvider
{
    /// <summary>
    /// Loads Cosmos schedule config or returns code defaults when the document is missing.
    /// Does not throw on missing document — schedule defaults keep the function host healthy.
    /// Lookback fail-closed is separate.
    /// </summary>
    Task<DiscoveryScheduleConfig> GetAsync(CancellationToken cancellationToken = default);
}

public class DiscoveryScheduleProvider(
    ILookupRepository lookupRepository,
    ILogger<DiscoveryScheduleProvider> logger) : IDiscoveryScheduleProvider
{
    public async Task<DiscoveryScheduleConfig> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var config = await lookupRepository.GetDiscoveryScheduleConfig();
            if (config is null)
            {
                logger.LogWarning(
                    "DiscoveryScheduleConfig LookUps document missing; using defaults runTimes=[{RunTimes}].",
                    string.Join(',', DiscoveryScheduleConfig.DefaultRunTimes));
                return DiscoveryScheduleConfig.CreateDefault();
            }

            if (config.RunTimes is null || config.RunTimes.Count == 0)
            {
                logger.LogWarning(
                    "DiscoveryScheduleConfig has empty runTimes; using defaults [{RunTimes}].",
                    string.Join(',', DiscoveryScheduleConfig.DefaultRunTimes));
                config.RunTimes = [.. DiscoveryScheduleConfig.DefaultRunTimes];
            }

            // Validate parse early so bad admin saves fail loudly on next get rather than at timer.
            _ = DiscoverySchedule.ParseRunTimes(config.RunTimes);
            return config;
        }
        catch (FormatException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to load DiscoveryScheduleConfig; using code defaults runTimes=[{RunTimes}].",
                string.Join(',', DiscoveryScheduleConfig.DefaultRunTimes));
            return DiscoveryScheduleConfig.CreateDefault();
        }
    }
}
