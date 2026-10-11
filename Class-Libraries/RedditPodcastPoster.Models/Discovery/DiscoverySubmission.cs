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

    [JsonPropertyName("history")]
    [JsonPropertyOrder(200)]
    public DiscoverySubmissionHistoryEntry[] History { get; set; } = [];
}

public enum DiscoverySubmissionStatus
{
    None = 0,
    Pending,
    Applied,
    ApplyFailed
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
