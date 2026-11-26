using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Discovery;

namespace Api.Models;

/// <summary>
/// Queued ingest of a claimed discovery submission (#1415). The apply / retry request has already validated the
/// submission, claimed it <c>Applying</c> and run the language / kind / identity preflight; the worker only ingests.
/// The submission document is the job record: <see cref="InProgressSince"/> identifies the claim this message
/// belongs to, so a stale or redelivered message never acts on a later claim.
/// </summary>
public sealed class DiscoverySubmissionApplyQueueMessage
{
    public const string QueueName = "discovery-submission-apply";

    /// <summary>Where the Functions host moves a message after <c>maxDequeueCount</c> failed attempts.</summary>
    public const string PoisonQueueName = QueueName + "-poison";

    [JsonPropertyName("submissionId")]
    public Guid SubmissionId { get; init; }

    [JsonPropertyName("operation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiscoverySubmissionOperation Operation { get; init; }

    /// <summary>The claim's <c>inProgressSince</c>.</summary>
    [JsonPropertyName("inProgressSince")]
    public DateTime InProgressSince { get; init; }

    [JsonPropertyName("enqueuedUtc")]
    public DateTime EnqueuedUtc { get; init; }

    [JsonPropertyName("languageDecisions")]
    public Dictionary<Guid, string>? LanguageDecisions { get; init; }

    [JsonPropertyName("identityDecisions")]
    public Dictionary<Guid, string>? IdentityDecisions { get; init; }

    [JsonPropertyName("selectedIdentityKeys")]
    public Dictionary<Guid, string>? SelectedIdentityKeys { get; init; }

    [JsonPropertyName("kindDecisions")]
    public Dictionary<Guid, string>? KindDecisions { get; init; }

    [JsonPropertyName("selectedKindKeys")]
    public Dictionary<Guid, string>? SelectedKindKeys { get; init; }
}
