using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Cosmos LookUps marker that one podcast kind transfer is running. Its id is the podcast id (LookUps documents are
/// partitioned by their own id), so a second transfer of the same podcast finds it and is refused. A marker older
/// than the stale window belongs to a transfer that died; the next transfer takes it over (etag-checked) and resumes.
/// </summary>
[CosmosSelector(ModelType.ParentKindTransferLock)]
public sealed class ParentKindTransferLock : CosmosSelector
{
    public ParentKindTransferLock()
    {
        ModelType = ModelType.ParentKindTransferLock;
    }

    [JsonPropertyName("targetKind")]
    [JsonPropertyOrder(10)]
    public CatalogueParentKind TargetKind { get; set; }

    [JsonPropertyName("startedUtc")]
    [JsonPropertyOrder(11)]
    public DateTime StartedUtc { get; set; }

    /// <summary>Identifies the transfer attempt that holds the marker.</summary>
    [JsonPropertyName("attemptId")]
    [JsonPropertyOrder(12)]
    public Guid AttemptId { get; set; }

    public override string FileKey => nameof(ParentKindTransferLock);
}
