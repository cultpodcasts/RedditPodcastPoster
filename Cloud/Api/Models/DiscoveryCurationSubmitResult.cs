using Api.Dtos;

namespace Api.Models;

public enum DiscoveryCurationSubmitStatus
{
    Ok,
    Failed
}

public record DiscoveryCurationSubmitResult(
    DiscoveryCurationSubmitStatus Status,
    DiscoverySubmitResponse? Response = null);
