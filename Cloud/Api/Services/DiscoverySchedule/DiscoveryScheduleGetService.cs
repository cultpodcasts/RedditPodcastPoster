using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery.Scheduling;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.DiscoverySchedule;

public interface IDiscoveryScheduleGetService
{
    Task<DiscoveryScheduleGetResult> GetAsync(CancellationToken cancellationToken);
}

public class DiscoveryScheduleGetService(
    ILookupRepository lookupRepository,
    ILogger<DiscoveryScheduleGetService> logger) : IDiscoveryScheduleGetService
{
    public async Task<DiscoveryScheduleGetResult> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var persisted = await lookupRepository.GetDiscoveryScheduleConfig();
            var config = persisted ?? DiscoveryScheduleConfig.CreateDefault();
            return new DiscoveryScheduleGetResult(
                DiscoveryScheduleGetStatus.Ok,
                config,
                IsDefault: persisted is null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to get DiscoveryScheduleConfig.");
            return new DiscoveryScheduleGetResult(DiscoveryScheduleGetStatus.Failed);
        }
    }
}
