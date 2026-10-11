namespace Api.Models;

public enum DiscoveryCurationSubmitStatus
{
    Ok,
    Failed,

    /// <summary>A requested report is held for supervisor review (or otherwise not curatable).</summary>
    Conflict
}

public record DiscoveryCurationSubmitResult(
    DiscoveryCurationSubmitStatus Status,
    DiscoverySubmitOutcome? Outcome = null);
