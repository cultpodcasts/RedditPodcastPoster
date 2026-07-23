using Api.Models;

namespace Api.Services.DiscoverySchedule;

public interface IDiscoveryScheduleUpdateService
{
    Task<DiscoveryScheduleUpdateResult> UpdateAsync(
        DiscoveryScheduleUpdateRequest body,
        CancellationToken cancellationToken);
}
