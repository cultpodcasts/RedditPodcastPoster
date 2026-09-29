using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Catalogue;

namespace Api.Dtos;

public sealed class PodcastKindTransferResponse
{
    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; init; }

    [JsonPropertyName("targetKind")]
    public CatalogueParentKind? TargetKind { get; init; }

    [JsonPropertyName("playableCount")]
    public int PlayableCount { get; init; }

    [JsonPropertyName("failureIndexingPlayables")]
    public bool FailureIndexingPlayables { get; init; }
}
