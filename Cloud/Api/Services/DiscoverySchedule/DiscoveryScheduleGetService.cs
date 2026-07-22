using Api.Dtos;
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
    private const int NextRunsPreviewCount = 6;

    public async Task<DiscoveryScheduleGetResult> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var persisted = await lookupRepository.GetDiscoveryScheduleConfig();
            var config = persisted ?? DiscoveryScheduleConfig.CreateDefault();
            var response = DiscoveryScheduleResponseBuilder.Build(config, isDefault: persisted is null, NextRunsPreviewCount);
            return new DiscoveryScheduleGetResult(DiscoveryScheduleGetStatus.Ok, response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to get DiscoveryScheduleConfig.");
            return new DiscoveryScheduleGetResult(DiscoveryScheduleGetStatus.Failed);
        }
    }
}
