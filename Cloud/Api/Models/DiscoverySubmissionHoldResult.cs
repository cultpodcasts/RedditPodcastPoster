namespace Api.Models;

public enum DiscoverySubmissionHoldStatus
{
    Held,
    Conflict,
    InvalidResultIds,
    Failed
}

public record DiscoverySubmissionHoldResult(
    DiscoverySubmissionHoldStatus Status,
    Guid? SubmissionId = null,
    IReadOnlyList<Guid>? UnavailableDocumentIds = null,
    IReadOnlyList<Guid>? UnknownResultIds = null);
