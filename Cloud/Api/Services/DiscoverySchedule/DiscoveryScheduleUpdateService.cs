using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using DiscoveryScheduleLogic = RedditPodcastPoster.Discovery.Scheduling.DiscoverySchedule;

namespace Api.Services.DiscoverySchedule;

public class DiscoveryScheduleUpdateService(
    ILookupRepository lookupRepository,
    ILogger<DiscoveryScheduleUpdateService> logger) : IDiscoveryScheduleUpdateService
{
    public async Task<DiscoveryScheduleUpdateResult> UpdateAsync(
        DiscoveryScheduleUpdateRequest body,
        CancellationToken cancellationToken)
    {
        try
        {
            if (body.RunTimes is null || body.RunTimes.Count == 0)
            {
                return new DiscoveryScheduleUpdateResult(
                    DiscoveryScheduleUpdateStatus.BadRequest,
                    Error: "runTimes must contain at least one HH:mm value on a 30-minute grid.");
            }

            IReadOnlyList<TimeOnly> parsed;
            try
            {
                parsed = DiscoveryScheduleLogic.ParseRunTimes(body.RunTimes);
            }
            catch (FormatException ex)
            {
                return new DiscoveryScheduleUpdateResult(
                    DiscoveryScheduleUpdateStatus.BadRequest,
                    Error: ex.Message);
            }

            var config = await lookupRepository.GetDiscoveryScheduleConfig() ?? new DiscoveryScheduleConfig();
            config.RunTimes = parsed.Select(t => t.ToString("HH\\:mm")).ToList();
            if (!string.IsNullOrWhiteSpace(body.TimeZoneId))
            {
                config.TimeZoneId = body.TimeZoneId.Trim();
            }

            if (body.Enabled is { } enabled)
            {
                config.Enabled = enabled;
            }

            DiscoveryScheduleLogic.ResolveUkTimeZone(config.TimeZoneId);

            await lookupRepository.SaveDiscoveryScheduleConfig(config);
            return new DiscoveryScheduleUpdateResult(DiscoveryScheduleUpdateStatus.Ok, config);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to update DiscoveryScheduleConfig.");
            return new DiscoveryScheduleUpdateResult(DiscoveryScheduleUpdateStatus.Failed);
        }
    }
}
