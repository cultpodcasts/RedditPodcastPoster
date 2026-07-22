namespace Api.Models;

public enum DiscoveryCurationSubmitStatus
{
    Ok,
    Failed
}

public record DiscoveryCurationSubmitResult(
    DiscoveryCurationSubmitStatus Status,
    DiscoverySubmitOutcome? Outcome = null);
