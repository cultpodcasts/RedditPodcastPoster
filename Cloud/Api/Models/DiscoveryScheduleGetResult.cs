using Api.Dtos;

namespace Api.Models;

public enum DiscoveryScheduleGetStatus
{
    Ok,
    Failed
}

public record DiscoveryScheduleGetResult(
    DiscoveryScheduleGetStatus Status,
    DiscoveryScheduleResponse? Response = null);
