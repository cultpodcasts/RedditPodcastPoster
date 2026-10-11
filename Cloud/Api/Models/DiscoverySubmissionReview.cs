using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

/// <summary>Who is calling the supervision endpoints, derived from token permissions.</summary>
public sealed record SupervisionCaller(string Subject, bool IsAdmin, bool IsSupervisor)
{
    public bool CanSuperviseAny => IsAdmin || IsSupervisor;
}

public sealed record DiscoverySubmissionItemCommentModel(Guid ResultId, string Comment);

/// <summary>Supervisor's draft review. <c>null</c> members are left unchanged.</summary>
public sealed class DiscoverySubmissionReviewRequest
{
    public required string ETag { get; init; }
    public string? ReportComment { get; init; }
    public IReadOnlyList<DiscoverySubmissionItemCommentModel>? ItemComments { get; init; }
    public Guid[]? AcceptedResultIds { get; init; }
}

public sealed class DiscoverySubmissionApplyRequest
{
    public required string ETag { get; init; }

    /// <summary>Final accepted set; when <c>null</c> the saved review set (or else the submitter's set) is used.</summary>
    public Guid[]? AcceptedResultIds { get; init; }
}

public sealed class DiscoverySubmissionRetryRequest
{
    public required string ETag { get; init; }

    /// <summary>Failed items to abandon (not retried) so the submission can close.</summary>
    public Guid[]? AbandonResultIds { get; init; }
}

public sealed class DiscoverySubmissionReleaseRequest
{
    public required string ETag { get; init; }
    public string? Reason { get; init; }
}

public sealed class DiscoveryReportReleaseRequest
{
    public string? Reason { get; init; }
}

public sealed record DiscoverySubmissionReviewRoute<T>(Guid SubmissionId, T Request);

public enum DiscoverySubmissionCommandStatus
{
    Ok,
    NotFound,
    Forbidden,

    /// <summary>ETag mismatch, wrong lifecycle state, or held reports no longer held for this submission.</summary>
    Conflict,

    /// <summary>Accepted ids outside the held reports, or malformed comments.</summary>
    Invalid,

    /// <summary>Apply ran and failed; submission is ApplyFailed and its reports remain PendingSupervisor.</summary>
    ApplyFailed
}

public sealed record DiscoverySubmissionCommandResult(
    DiscoverySubmissionCommandStatus Status,
    string? Message = null,
    string? ETag = null,
    DiscoverySubmissionStatus? SubmissionStatus = null,
    DiscoverySubmissionDiff? Diff = null,
    DiscoverySubmitOutcome? Outcome = null,
    IReadOnlyList<Guid>? InvalidIds = null,
    IReadOnlyList<DiscoverySubmissionItemOutcome>? ItemOutcomes = null);

public sealed record DiscoverySubmissionDiff(Guid[] Added, Guid[] Removed);

/// <summary>Read model for one submission, with the full held reports so a supervisor can rework it.</summary>
public sealed record DiscoverySubmissionDetail(
    DiscoverySubmission Submission,
    IReadOnlyList<DiscoveryResultsDocument> Reports,
    IReadOnlyDictionary<Guid, DiscoveryPodcastInfo> Podcasts,
    bool CanReview);

public sealed record DiscoverySubmissionList(
    IReadOnlyList<DiscoverySubmission> Submissions,
    int PendingCount,
    bool IsSupervisorView);

public enum DiscoverySubmissionQueryStatus
{
    Ok,
    NotFound,
    Forbidden,
    Failed
}

public sealed record DiscoverySubmissionQueryResult<T>(DiscoverySubmissionQueryStatus Status, T? Value = default);
