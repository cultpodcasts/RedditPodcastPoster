using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos;

public sealed class PodcastKindTransferJobStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "Idle";

    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; init; }

    [JsonPropertyName("targetKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CatalogueParentKind? TargetKind { get; init; }

    [JsonPropertyName("playableCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int PlayableCount { get; init; }

    [JsonPropertyName("failureIndexingPlayables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FailureIndexingPlayables { get; init; }

    [JsonPropertyName("failureReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FailureReason { get; init; }

    [JsonPropertyName("enqueuedUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EnqueuedUtc { get; init; }

    [JsonPropertyName("startedUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? StartedUtc { get; init; }

    [JsonPropertyName("completedUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? CompletedUtc { get; init; }
}
