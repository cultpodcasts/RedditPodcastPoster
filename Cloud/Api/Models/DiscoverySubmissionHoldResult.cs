namespace Api.Models;

public enum DiscoverySubmissionHoldStatus
{
    Held,
    Conflict,
    Failed
}

public record DiscoverySubmissionHoldResult(
    DiscoverySubmissionHoldStatus Status,
    Guid? SubmissionId = null,
    IReadOnlyList<Guid>? UnavailableDocumentIds = null);
