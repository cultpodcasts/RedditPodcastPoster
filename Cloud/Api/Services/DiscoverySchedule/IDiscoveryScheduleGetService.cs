using Api.Models;

namespace Api.Services.DiscoverySchedule;

public interface IDiscoveryScheduleGetService
{
    Task<DiscoveryScheduleGetResult> GetAsync(CancellationToken cancellationToken);
}
