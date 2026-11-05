using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos;

public sealed class PodcastKindTransferEnqueueResponse
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = "Queued";

    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; init; }

    [JsonPropertyName("targetKind")]
    public CatalogueParentKind? TargetKind { get; init; }

    [JsonPropertyName("enqueuedUtc")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? EnqueuedUtc { get; init; }

    /// <summary>Relative path clients poll for job progress (<c>GET podcast/{id}/kind</c>).</summary>
    [JsonPropertyName("statusPath")]
    public string? StatusPath { get; init; }
}
