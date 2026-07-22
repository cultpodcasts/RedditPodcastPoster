using Api.Dtos;

namespace Api.Models;

public enum DiscoveryScheduleUpdateStatus
{
    Ok,
    BadRequest,
    Failed
}

public record DiscoveryScheduleUpdateResult(
    DiscoveryScheduleUpdateStatus Status,
    DiscoveryScheduleResponse? Response = null,
    string? Error = null);
