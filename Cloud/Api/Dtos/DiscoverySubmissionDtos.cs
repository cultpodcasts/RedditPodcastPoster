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

    /// <summary>Items that failed to ingest and await retry/abandon. <c>null</c> until an apply attempted ingestion.</summary>
    [JsonPropertyName("failedCount")]
    [JsonPropertyOrder(120)]
    public int? FailedCount { get; set; }

    [JsonPropertyName("succeededCount")]
    [JsonPropertyOrder(130)]
    public int? SucceededCount { get; set; }

    [JsonPropertyName("abandonedCount")]
    [JsonPropertyOrder(140)]
    public int? AbandonedCount { get; set; }

    [JsonPropertyName("releasedAt")]
    [JsonPropertyOrder(150)]
    public DateTime? ReleasedAt { get; set; }

    [JsonPropertyName("releaseReason")]
    [JsonPropertyOrder(160)]
    public string? ReleaseReason { get; set; }

    /// <summary>While status is Applying: Apply | Retry | Release. <c>null</c> otherwise.</summary>
    [JsonPropertyName("inProgressOperation")]
    [JsonPropertyOrder(170)]
    public string? InProgressOperation { get; set; }

    [JsonPropertyName("inProgressSince")]
    [JsonPropertyOrder(180)]
    public DateTime? InProgressSince { get; set; }
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

    /// <summary>Per-result apply/retry outcome with failure reasons. <c>null</c> until an apply attempted ingestion.</summary>
    [JsonPropertyName("itemOutcomes")]
    [JsonPropertyOrder(165)]
    public DiscoverySubmissionItemOutcomeDto[]? ItemOutcomes { get; set; }

    [JsonPropertyName("releasedAt")]
    [JsonPropertyOrder(166)]
    public DateTime? ReleasedAt { get; set; }

    [JsonPropertyName("releaseReason")]
    [JsonPropertyOrder(167)]
    public string? ReleaseReason { get; set; }

    [JsonPropertyName("inProgressOperation")]
    [JsonPropertyOrder(168)]
    public string? InProgressOperation { get; set; }

    [JsonPropertyName("inProgressSince")]
    [JsonPropertyOrder(169)]
    public DateTime? InProgressSince { get; set; }

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

public class DiscoverySubmissionItemOutcomeDto
{
    [JsonPropertyName("resultId")]
    [JsonPropertyOrder(10)]
    public required Guid ResultId { get; set; }

    /// <summary>Succeeded | Failed | Abandoned.</summary>
    [JsonPropertyName("state")]
    [JsonPropertyOrder(20)]
    public required string State { get; set; }

    [JsonPropertyName("episodeId")]
    [JsonPropertyOrder(30)]
    public Guid? EpisodeId { get; set; }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(40)]
    public Guid? PodcastId { get; set; }

    /// <summary>Submit outcome from the ingester (e.g. Created, Enriched, Error).</summary>
    [JsonPropertyName("message")]
    [JsonPropertyOrder(50)]
    public string? Message { get; set; }

    /// <summary>Why it failed. <c>null</c> unless the last attempt failed.</summary>
    [JsonPropertyName("error")]
    [JsonPropertyOrder(60)]
    public string? Error { get; set; }

    [JsonPropertyName("attempts")]
    [JsonPropertyOrder(70)]
    public required int Attempts { get; set; }

    [JsonPropertyName("lastAttemptAt")]
    [JsonPropertyOrder(80)]
    public DateTime? LastAttemptAt { get; set; }

    /// <summary><c>NewsReport</c> or <c>TvShowEpisode</c> for an item stored under a news organisation / TV show.</summary>
    [JsonPropertyName("contentKind")]
    [JsonPropertyOrder(90)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentKind { get; set; }
}

/// <summary>POST DiscoverySubmissions/{id}/retry.</summary>
public class DiscoverySubmissionRetryRequestDto
{
    [JsonPropertyName("etag")]
    public string ETag { get; set; } = "";

    /// <summary>Failed items to abandon instead of retrying.</summary>
    [JsonPropertyName("abandonResultIds")]
    public Guid[]? AbandonResultIds { get; set; }

    /// <summary>Discovery result id to <c>add</c> or <c>continue</c> after an unsupported-language prompt.</summary>
    [JsonPropertyName("languageDecisions")]
    public Dictionary<Guid, string>? LanguageDecisions { get; set; }
}

/// <summary>POST DiscoverySubmissions/{id}/release.</summary>
public class DiscoverySubmissionReleaseRequestDto
{
    [JsonPropertyName("etag")]
    public string ETag { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>POST DiscoverySubmissions/{id}/state (admin): manual status override.</summary>
public class DiscoverySubmissionSetStateRequestDto
{
    /// <summary>A DiscoverySubmissionStatus name (case-insensitive), e.g. "Pending"; "None", "Applying" and numbers are rejected.</summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("etag")]
    public string ETag { get; set; } = "";

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>POST DiscoveryReports/{documentId}/release (admin).</summary>
public class DiscoveryReportReleaseRequestDto
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>Command outcome for an admin stuck-report release.</summary>
public class DiscoveryReportReleaseResponse
{
    [JsonPropertyName("documentId")]
    [JsonPropertyOrder(10)]
    public required Guid DocumentId { get; set; }

    /// <summary>Released | Conflict | NotFound | Forbidden.</summary>
    [JsonPropertyName("status")]
    [JsonPropertyOrder(20)]
    public required string Status { get; set; }

    [JsonPropertyName("message")]
    [JsonPropertyOrder(30)]
    public string? Message { get; set; }

    /// <summary>On conflict: live submission ids still holding the report.</summary>
    [JsonPropertyName("submissionIds")]
    [JsonPropertyOrder(40)]
    public Guid[]? SubmissionIds { get; set; }
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

    /// <summary>StateChanged only: previous status.</summary>
    [JsonPropertyName("fromStatus")]
    [JsonPropertyOrder(40)]
    public string? FromStatus { get; set; }

    /// <summary>StateChanged only: new status.</summary>
    [JsonPropertyName("toStatus")]
    [JsonPropertyOrder(50)]
    public string? ToStatus { get; set; }

    [JsonPropertyName("reason")]
    [JsonPropertyOrder(60)]
    public string? Reason { get; set; }
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

    /// <summary>Discovery result id to <c>add</c> or <c>continue</c> after an unsupported-language prompt.</summary>
    [JsonPropertyName("languageDecisions")]
    public Dictionary<Guid, string>? LanguageDecisions { get; set; }
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

    /// <summary>Ids the command could not act on (invalid result ids, unheld reports, …). <c>null</c> when none.</summary>
    [JsonPropertyName("ids")]
    [JsonPropertyOrder(65)]
    public Guid[]? Ids { get; set; }

    /// <summary>Apply/retry: this run's per-result ingest outcome (same shape as POST DiscoveryCuration response).</summary>
    [JsonPropertyName("outcome")]
    [JsonPropertyOrder(70)]
    public DiscoverySubmitResponse? Outcome { get; set; }

    /// <summary>Apply/retry: cumulative per-result outcomes with failure reasons. <c>null</c> otherwise.</summary>
    [JsonPropertyName("itemOutcomes")]
    [JsonPropertyOrder(80)]
    public DiscoverySubmissionItemOutcomeDto[]? ItemOutcomes { get; set; }
}
