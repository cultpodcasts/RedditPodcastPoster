using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// LookUps record of the latest podcast kind-transfer job for a source podcast. Id is a
/// deterministic Guid derived from the podcast id (xor with a fixed namespace) so it never
/// collides with the single-flight <see cref="ParentKindTransferLock"/> marker, which uses the
/// podcast id itself.
/// </summary>
[CosmosSelector(ModelType.ParentKindTransferJob)]
public sealed class ParentKindTransferJob : CosmosSelector, IFileKeyed
{
    /// <summary>Namespace xor'd with the podcast id to form this document's Cosmos id.</summary>
    public static readonly Guid IdNamespace = Guid.Parse("b3e1c9a0-4f62-4d8e-9a17-6c2d8f0e5b41");

    public ParentKindTransferJob()
    {
        ModelType = ModelType.ParentKindTransferJob;
    }

    public static Guid IdFor(Guid podcastId)
    {
        var podcastBytes = podcastId.ToByteArray();
        var namespaceBytes = IdNamespace.ToByteArray();
        for (var i = 0; i < podcastBytes.Length; i++)
        {
            podcastBytes[i] ^= namespaceBytes[i];
        }

        return new Guid(podcastBytes);
    }

    [JsonPropertyName("podcastId")]
    [JsonPropertyOrder(10)]
    public Guid PodcastId { get; set; }

    [JsonPropertyName("targetKind")]
    [JsonPropertyOrder(11)]
    public CatalogueParentKind TargetKind { get; set; }

    [JsonPropertyName("phase")]
    [JsonPropertyOrder(12)]
    public ParentKindTransferJobPhase Phase { get; set; }

    [JsonPropertyName("enqueuedUtc")]
    [JsonPropertyOrder(13)]
    public DateTime EnqueuedUtc { get; set; }

    [JsonPropertyName("startedUtc")]
    [JsonPropertyOrder(14)]
    public DateTime? StartedUtc { get; set; }

    [JsonPropertyName("completedUtc")]
    [JsonPropertyOrder(15)]
    public DateTime? CompletedUtc { get; set; }

    [JsonPropertyName("playableCount")]
    [JsonPropertyOrder(16)]
    public int PlayableCount { get; set; }

    [JsonPropertyName("failureIndexingPlayables")]
    [JsonPropertyOrder(17)]
    public bool FailureIndexingPlayables { get; set; }

    [JsonPropertyName("failureReason")]
    [JsonPropertyOrder(18)]
    public string? FailureReason { get; set; }

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(ParentKindTransferJob);
}

public enum ParentKindTransferJobPhase
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Incomplete = 4
}
