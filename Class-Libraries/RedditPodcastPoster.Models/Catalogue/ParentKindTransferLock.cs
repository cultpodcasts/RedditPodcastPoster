using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Cosmos LookUps marker that one podcast kind transfer is running. Its id is the podcast id (LookUps documents are
/// partitioned by their own id), so a second transfer of the same podcast finds it and is refused. A marker older
/// than the stale window (measured from its last heartbeat) belongs to a transfer that died; the next transfer takes
/// it over (etag-checked) and resumes. LookUps has container TTL enabled with defaultTtl -1; this marker sets
/// <see cref="TimeToLiveSeconds"/> to 2× the stale window so a crashed holder's leftover expires on its own.
/// </summary>
[CosmosSelector(ModelType.ParentKindTransferLock)]
public sealed class ParentKindTransferLock : CosmosSelector, IFileKeyed
{
    public ParentKindTransferLock()
    {
        ModelType = ModelType.ParentKindTransferLock;
    }

    /// <summary>Cosmos item TTL (seconds from last write) for a marker: twice the stale window.</summary>
    public static int TimeToLiveSecondsFor(TimeSpan staleAfter) =>
        checked((int)(staleAfter.TotalSeconds * 2));

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

    /// <summary>Last renewal by the holder; staleness is measured from here (or <see cref="StartedUtc"/>).</summary>
    [JsonPropertyName("heartbeatUtc")]
    [JsonPropertyOrder(13)]
    public DateTime? HeartbeatUtc { get; set; }

    /// <summary>
    /// Cosmos per-item TTL (seconds). Refreshed on create, takeover, and each heartbeat so an abandoned marker
    /// expires about 2× the stale window after the last write.
    /// </summary>
    [JsonPropertyName("ttl")]
    [JsonPropertyOrder(14)]
    public int? TimeToLiveSeconds { get; set; }

    [JsonIgnore]
    public DateTime LastAliveUtc => HeartbeatUtc ?? StartedUtc;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(ParentKindTransferLock);
}
