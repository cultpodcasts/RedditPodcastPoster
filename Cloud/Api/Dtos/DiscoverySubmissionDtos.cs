using System.Text.Json.Serialization;

namespace Api.Dtos;

/// <summary>GET DiscoverySubmissions — supervisor queue or the caller's own submissions.</summary>
public class DiscoverySubmissionListResponse
{
    /// <summary>"supervisor" (reviewable queue) or "submitter" (caller's own submissions).</summary>
    [JsonPropertyName("view")]
    [JsonPropertyOrder(10)]
    public required string View { get; set; }

    /// <summary>Supervisor view: submissions awaiting the caller (Pending + ApplyFailed). Submitter view: own Pending.</summary>
    [JsonPropertyName("pendingCount")]
    [JsonPropertyOrder(20)]
    public required int PendingCount { get; set; }

    [JsonPropertyName("submissions")]
    [JsonPropertyOrder(30)]
    public required IEnumerable<DiscoverySubmissionSummaryDto> Submissions { get; set; }
}

public class DiscoverySubmissionSummaryDto
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public required Guid Id { get; set; }

    /// <summary>Pending | Applied | ApplyFailed.</summary>
    [JsonPropertyName("status")]
    [JsonPropertyOrder(20)]
    public required string Status { get; set; }

    [JsonPropertyName("submitterSubject")]
    [JsonPropertyOrder(30)]
    public required string SubmitterSubject { get; set; }

    [JsonPropertyName("submittedAt")]
    [JsonPropertyOrder(40)]
    public required DateTime SubmittedAt { get; set; }

    [JsonPropertyName("discoveryResultsDocumentIds")]
    [JsonPropertyOrder(50)]
    public required Guid[] DiscoveryResultsDocumentIds { get; set; }

    [JsonPropertyName("submitterAcceptedCount")]
    [JsonPropertyOrder(60)]
    public required int SubmitterAcceptedCount { get; set; }

    [JsonPropertyName("supervisorSubject")]
    [JsonPropertyOrder(70)]
    public string? SupervisorSubject { get; set; }

    [JsonPropertyName("appliedAt")]
    [JsonPropertyOrder(80)]
    public DateTime? AppliedAt { get; set; }

    [JsonPropertyName("reportComment")]
    [JsonPropertyOrder(90)]
    public string? ReportComment { get; set; }

    [JsonPropertyName("itemCommentCount")]
    [JsonPropertyOrder(100)]
    public required int ItemCommentCount { get; set; }

    [JsonPropertyName("applyError")]
    [JsonPropertyOrder(110)]
    public string? ApplyError { get; set; }
}

/// <summary>GET DiscoverySubmissions/{id} — the submission plus its held reports' full results.</summary>
public class DiscoverySubmissionDetailResponse
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public required Guid Id { get; set; }

    [JsonPropertyName("status")]
    [JsonPropertyOrder(20)]
    public required string Status { get; set; }

    /// <summary>Opaque concurrency token; send back on review/apply.</summary>
    [JsonPropertyName("etag")]
    [JsonPropertyOrder(30)]
    public required string ETag { get; set; }

    /// <summary>True when the caller may save a review / apply this submission now.</summary>
    [JsonPropertyName("canReview")]
    [JsonPropertyOrder(40)]
    public required bool CanReview { get; set; }

    [JsonPropertyName("submitterSubject")]
    [JsonPropertyOrder(50)]
    public required string SubmitterSubject { get; set; }

    [JsonPropertyName("submittedAt")]
    [JsonPropertyOrder(60)]
    public required DateTime SubmittedAt { get; set; }

    [JsonPropertyName("submitterAcceptedResultIds")]
    [JsonPropertyOrder(70)]
    public required Guid[] SubmitterAcceptedResultIds { get; set; }

    /// <summary><c>null</c> until a supervisor saves a review or applies.</summary>
    [JsonPropertyName("supervisorAcceptedResultIds")]
    [JsonPropertyOrder(80)]
    public Guid[]? SupervisorAcceptedResultIds { get; set; }

    [JsonPropertyName("addedBySupervisorResultIds")]
    [JsonPropertyOrder(90)]
    public Guid[]? AddedBySupervisorResultIds { get; set; }

    [JsonPropertyName("removedBySupervisorResultIds")]
    [JsonPropertyOrder(100)]
    public Guid[]? RemovedBySupervisorResultIds { get; set; }

    [JsonPropertyName("reportComment")]
    [JsonPropertyOrder(110)]
    public string? ReportComment { get; set; }

    [JsonPropertyName("itemComments")]
    [JsonPropertyOrder(120)]
    public required IEnumerable<DiscoverySubmissionItemCommentDto> ItemComments { get; set; }

    [JsonPropertyName("supervisorSubject")]
    [JsonPropertyOrder(130)]
    public string? SupervisorSubject { get; set; }

    [JsonPropertyName("appliedAt")]
    [JsonPropertyOrder(140)]
    public DateTime? AppliedAt { get; set; }

    [JsonPropertyName("applyError")]
    [JsonPropertyOrder(150)]
    public string? ApplyError { get; set; }

    [JsonPropertyName("erroredResultIds")]
    [JsonPropertyOrder(160)]
    public Guid[]? ErroredResultIds { get; set; }

    [JsonPropertyName("reports")]
    [JsonPropertyOrder(170)]
    public required IEnumerable<DiscoverySubmissionReportDto> Reports { get; set; }

    [JsonPropertyName("history")]
    [JsonPropertyOrder(180)]
    public required IEnumerable<DiscoverySubmissionHistoryDto> History { get; set; }
}

public class DiscoverySubmissionReportDto
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(10)]
    public required Guid Id { get; set; }

    [JsonPropertyName("discoveryBegan")]
    [JsonPropertyOrder(20)]
    public required DateTime DiscoveryBegan { get; set; }

    /// <summary>Unprocessed | PendingSupervisor | Processed.</summary>
    [JsonPropertyName("state")]
    [JsonPropertyOrder(30)]
    public required string State { get; set; }

    /// <summary>Every result in the report (same item shape as GET DiscoveryCuration), including auto-hidden.</summary>
    [JsonPropertyName("results")]
    [JsonPropertyOrder(40)]
    public required IEnumerable<DiscoveryResponse.Item> Results { get; set; }
}

public class DiscoverySubmissionItemCommentDto
{
    [JsonPropertyName("resultId")]
    [JsonPropertyOrder(10)]
    public Guid ResultId { get; set; }

    [JsonPropertyName("comment")]
    [JsonPropertyOrder(20)]
    public string Comment { get; set; } = "";
}

public class DiscoverySubmissionHistoryDto
{
    [JsonPropertyName("at")]
    [JsonPropertyOrder(10)]
    public required DateTime At { get; set; }

    [JsonPropertyName("actorSubject")]
    [JsonPropertyOrder(20)]
    public string? ActorSubject { get; set; }

    [JsonPropertyName("action")]
    [JsonPropertyOrder(30)]
    public required string Action { get; set; }
}

/// <summary>PUT DiscoverySubmissions/{id}/review — save a draft review. Omitted (null) members are unchanged.</summary>
public class DiscoverySubmissionReviewRequestDto
{
    [JsonPropertyName("etag")]
    public string ETag { get; set; } = "";

    /// <summary>Report-level comment. Empty string clears it.</summary>
    [JsonPropertyName("reportComment")]
    public string? ReportComment { get; set; }

    /// <summary>Replaces all item comments. Blank comments are dropped; [] clears.</summary>
    [JsonPropertyName("itemComments")]
    public DiscoverySubmissionItemCommentDto[]? ItemComments { get; set; }

    /// <summary>Supervisor's final accepted set: any result ids within the held reports.</summary>
    [JsonPropertyName("acceptedResultIds")]
    public Guid[]? AcceptedResultIds { get; set; }
}

/// <summary>POST DiscoverySubmissions/{id}/apply.</summary>
public class DiscoverySubmissionApplyRequestDto
{
    [JsonPropertyName("etag")]
    public string ETag { get; set; } = "";

    /// <summary>Optional final accepted set; when omitted the saved review set (else the submitter's set) applies.</summary>
    [JsonPropertyName("acceptedResultIds")]
    public Guid[]? AcceptedResultIds { get; set; }
}

/// <summary>Command outcome for review save / apply. Not the submission read model — GET it to rebind.</summary>
public class DiscoverySubmissionCommandResponse
{
    [JsonPropertyName("submissionId")]
    [JsonPropertyOrder(10)]
    public required Guid SubmissionId { get; set; }

    [JsonPropertyName("status")]
    [JsonPropertyOrder(20)]
    public required string Status { get; set; }

    /// <summary>New concurrency token, for a further review/apply without re-reading.</summary>
    [JsonPropertyName("etag")]
    [JsonPropertyOrder(30)]
    public string? ETag { get; set; }

    [JsonPropertyName("addedBySupervisorResultIds")]
    [JsonPropertyOrder(40)]
    public Guid[]? AddedBySupervisorResultIds { get; set; }

    [JsonPropertyName("removedBySupervisorResultIds")]
    [JsonPropertyOrder(50)]
    public Guid[]? RemovedBySupervisorResultIds { get; set; }

    [JsonPropertyName("message")]
    [JsonPropertyOrder(60)]
    public string? Message { get; set; }

    /// <summary>Apply only: per-result ingest outcome (same shape as POST DiscoveryCuration response).</summary>
    [JsonPropertyName("outcome")]
    [JsonPropertyOrder(70)]
    public DiscoverySubmitResponse? Outcome { get; set; }
}
