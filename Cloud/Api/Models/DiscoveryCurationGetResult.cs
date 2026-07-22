using Api.Dtos;

namespace Api.Models;

public enum DiscoveryCurationGetStatus
{
    Ok,
    Failed
}

public record DiscoveryCurationGetResult(
    DiscoveryCurationGetStatus Status,
    DiscoveryResponse? Response = null);
