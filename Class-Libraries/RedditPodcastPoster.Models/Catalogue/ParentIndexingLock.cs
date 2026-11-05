using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Cosmos LookUps marker that one parent (podcast / TV show / news organisation) is being indexed. Its id is
/// <see cref="IdFor"/> of the parent id (xor with <see cref="IdNamespace"/>), so it never collides with the kind-transfer
/// lock which uses the parent id itself. A marker older than the stale window (measured from its last heartbeat)
/// belongs to an index that died; the next index takes it over (etag-checked).
/// </summary>
[CosmosSelector(ModelType.ParentIndexingLock)]
public sealed class ParentIndexingLock : CosmosSelector, IFileKeyed
{
    /// <summary>Namespace xor'd with the parent id to form this document's Cosmos id.</summary>
    public static readonly Guid IdNamespace = Guid.Parse("a1c4e8f2-7b39-4d6a-9e05-3f8c1d2a6b70");

    public const string SourceOnDemand = "OnDemand";
    public const string SourceHourly = "Hourly";

    public ParentIndexingLock()
    {
        ModelType = ModelType.ParentIndexingLock;
    }

    public static Guid IdFor(Guid parentId)
    {
        var parentBytes = parentId.ToByteArray();
        var namespaceBytes = IdNamespace.ToByteArray();
        for (var i = 0; i < parentBytes.Length; i++)
        {
            parentBytes[i] ^= namespaceBytes[i];
        }

        return new Guid(parentBytes);
    }

    /// <summary>The real parent (podcast / TV show / news organisation) guid — not the Cosmos document id.</summary>
    [JsonPropertyName("parentId")]
    [JsonPropertyOrder(10)]
    public Guid ParentId { get; set; }

    [JsonPropertyName("startedUtc")]
    [JsonPropertyOrder(11)]
    public DateTime StartedUtc { get; set; }

    /// <summary>Identifies the index attempt that holds the marker.</summary>
    [JsonPropertyName("attemptId")]
    [JsonPropertyOrder(12)]
    public Guid AttemptId { get; set; }

    /// <summary>Last renewal by the holder; staleness is measured from here (or <see cref="StartedUtc"/>).</summary>
    [JsonPropertyName("heartbeatUtc")]
    [JsonPropertyOrder(13)]
    public DateTime? HeartbeatUtc { get; set; }

    /// <summary><see cref="SourceOnDemand"/> or <see cref="SourceHourly"/>.</summary>
    [JsonPropertyName("source")]
    [JsonPropertyOrder(14)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; set; }

    [JsonIgnore]
    public DateTime LastAliveUtc => HeartbeatUtc ?? StartedUtc;

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(ParentIndexingLock);
}
