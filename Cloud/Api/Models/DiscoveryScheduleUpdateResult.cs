namespace Api.Models;

public enum DiscoveryScheduleUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record DiscoveryScheduleUpdateResult(
    DiscoveryScheduleUpdateStatus Status,
    string? Error = null);
