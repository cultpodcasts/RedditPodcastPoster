using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Discovery;

/// <summary>
/// A discovery-curation submit made by a supervised curator, held (not acted on) until a supervisor applies it.
/// Keeps the submitter's original accepted set and (once reviewed) the supervisor's final set plus the diff,
/// so supervisor corrections can be fed back to the submitting agent.
/// </summary>
[CosmosSelector(ModelType.DiscoverySubmission)]
public sealed class DiscoverySubmission : CosmosSelector
{
    public DiscoverySubmission()
    {
        ModelType = ModelType.DiscoverySubmission;
    }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(10)]
    public DiscoverySubmissionStatus Status { get; set; }

    [JsonPropertyName("submitterSubject")]
    [JsonPropertyOrder(20)]
    public required string SubmitterSubject { get; set; }

    [JsonPropertyName("submittedAt")]
    [JsonPropertyOrder(30)]
    public DateTime SubmittedAt { get; set; }

    /// <summary>Discovery reports (DiscoveryResultsDocument ids) held by this submission.</summary>
    [JsonPropertyName("discoveryResultsDocumentIds")]
    [JsonPropertyOrder(40)]
    public Guid[] DiscoveryResultsDocumentIds { get; set; } = [];

    /// <summary>Result ids the submitter accepted. Every other result in the held reports was not accepted.</summary>
    [JsonPropertyName("submitterAcceptedResultIds")]
    [JsonPropertyOrder(50)]
    public Guid[] SubmitterAcceptedResultIds { get; set; } = [];

    /// <summary>Supervisor's final accepted set. <c>null</c> until reviewed.</summary>
    [JsonPropertyName("supervisorAcceptedResultIds")]
    [JsonPropertyOrder(60)]
    public Guid[]? SupervisorAcceptedResultIds { get; set; }

    /// <summary>Accepted by the supervisor but not the submitter. <c>null</c> until reviewed; <c>[]</c> when none.</summary>
    [JsonPropertyName("addedBySupervisorResultIds")]
    [JsonPropertyOrder(70)]
    public Guid[]? AddedBySupervisorResultIds { get; set; }

    /// <summary>Accepted by the submitter but removed by the supervisor. <c>null</c> until reviewed; <c>[]</c> when none.</summary>
    [JsonPropertyName("removedBySupervisorResultIds")]
    [JsonPropertyOrder(80)]
    public Guid[]? RemovedBySupervisorResultIds { get; set; }

    [JsonPropertyName("reportComment")]
    [JsonPropertyOrder(90)]
    public string? ReportComment { get; set; }

    /// <summary>Supervisor comments on individual results. <c>null</c> until any are set.</summary>
    [JsonPropertyName("itemComments")]
    [JsonPropertyOrder(100)]
    public DiscoverySubmissionItemComment[]? ItemComments { get; set; }

    [JsonPropertyName("supervisorSubject")]
    [JsonPropertyOrder(110)]
    public string? SupervisorSubject { get; set; }

    [JsonPropertyName("appliedAt")]
    [JsonPropertyOrder(120)]
    public DateTime? AppliedAt { get; set; }

    /// <summary>
    /// Supervisor subjects mapped to the submitter (Auth0 <c>https://api.cultpodcasts.com/supervisors</c> claim)
    /// captured at submit. <c>null</c>/empty: any supervisor may review. Admins may always review.
    /// </summary>
    [JsonPropertyName("supervisorSubjects")]
    [JsonPropertyOrder(130)]
    public string[]? SupervisorSubjects { get; set; }

    /// <summary>Why the last apply failed. <c>null</c> unless <see cref="Status"/> is <c>ApplyFailed</c>.</summary>
    [JsonPropertyName("applyError")]
    [JsonPropertyOrder(140)]
    public string? ApplyError { get; set; }

    /// <summary>Number of results ingested by the apply (accepted minus errored). <c>null</c> until applied.</summary>
    [JsonPropertyName("appliedResultCount")]
    [JsonPropertyOrder(150)]
    public int? AppliedResultCount { get; set; }

    /// <summary>Results whose ingest errored during apply. <c>null</c> until applied.</summary>
    [JsonPropertyName("erroredResultIds")]
    [JsonPropertyOrder(160)]
    public Guid[]? ErroredResultIds { get; set; }

    /// <summary>
    /// Per-result outcome of apply/retry for every result in the applied final set. <c>null</c> until an apply
    /// attempted ingestion.
    /// </summary>
    [JsonPropertyName("itemOutcomes")]
    [JsonPropertyOrder(170)]
    public DiscoverySubmissionItemOutcome[]? ItemOutcomes { get; set; }

    [JsonPropertyName("releasedAt")]
    [JsonPropertyOrder(180)]
    public DateTime? ReleasedAt { get; set; }

    [JsonPropertyName("releaseReason")]
    [JsonPropertyOrder(190)]
    public string? ReleaseReason { get; set; }

    [JsonPropertyName("history")]
    [JsonPropertyOrder(200)]
    public DiscoverySubmissionHistoryEntry[] History { get; set; } = [];

    /// <summary>Cosmos ETag; read-only system property used for optimistic concurrency.</summary>
    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
}

public enum DiscoverySubmissionStatus
{
    None = 0,
    Pending,
    Applied,
    ApplyFailed,

    /// <summary>Apply ingested some results but others failed; failed items await retry or abandonment.</summary>
    PartiallyApplied,

    /// <summary>Terminal: a supervisor released the held reports back to Unprocessed; nothing was ingested.</summary>
    Released
}

public enum DiscoverySubmissionItemState
{
    None = 0,
    Succeeded,
    Failed,

    /// <summary>A failed item the supervisor chose not to retry.</summary>
    Abandoned
}

public sealed class DiscoverySubmissionItemOutcome
{
    [JsonPropertyName("resultId")]
    [JsonPropertyOrder(10)]
    public Guid ResultId { get; set; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonPropertyOrder(20)]
    public DiscoverySubmissionItemState State { get; set; }

    [JsonPropertyName("episodeId")]
    [JsonPropertyOrder(30)]
    public Guid? EpisodeId { get; set; }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(40)]
    public Guid? PodcastId { get; set; }

    /// <summary>Submit outcome (e.g. Created/Enriched/Error) from the URL submitter.</summary>
    [JsonPropertyName("message")]
    [JsonPropertyOrder(50)]
    public string? Message { get; set; }

    /// <summary>Why it failed. <c>null</c> on success.</summary>
    [JsonPropertyName("error")]
    [JsonPropertyOrder(60)]
    public string? Error { get; set; }

    [JsonPropertyName("attempts")]
    [JsonPropertyOrder(70)]
    public int Attempts { get; set; }

    [JsonPropertyName("lastAttemptAt")]
    [JsonPropertyOrder(80)]
    public DateTime? LastAttemptAt { get; set; }
}

public sealed class DiscoverySubmissionItemComment
{
    [JsonPropertyName("resultId")]
    [JsonPropertyOrder(10)]
    public Guid ResultId { get; set; }

    [JsonPropertyName("comment")]
    [JsonPropertyOrder(20)]
    public required string Comment { get; set; }
}

public sealed class DiscoverySubmissionHistoryEntry
{
    [JsonPropertyName("at")]
    [JsonPropertyOrder(10)]
    public DateTime At { get; set; }

    [JsonPropertyName("actorSubject")]
    [JsonPropertyOrder(20)]
    public string? ActorSubject { get; set; }

    [JsonPropertyName("action")]
    [JsonPropertyOrder(30)]
    public required string Action { get; set; }
}
