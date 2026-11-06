using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Cosmos;

/// <summary>
/// LookUps marker that a file key is taken. Id is <c>filekey:{lowercased key}</c>, partitioned by that id.
/// Created with <c>CreateItemAsync</c>; a 409 means another owner holds it. <see cref="ScopeId"/> is the
/// allocator scope that may release an uncommitted marker. A save of the podcast, TV show, news organisation
/// or film keeps the marker. A create that is never saved releases it when that scope still holds the lease.
/// An abandoned marker (process killed before release or save) expires after <see cref="UncommittedLifetimeSeconds"/>.
/// </summary>
public sealed class FileKeyClaim : IFileKeyed
{
    public const string IdPrefix = "filekey:";

    /// <summary>Cosmos item TTL for a claim that has not been kept by a save.</summary>
    public const int UncommittedLifetimeSeconds = 24 * 60 * 60;

    public static string DocumentIdFor(string fileKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileKey);
        return IdPrefix + fileKey.ToLowerInvariant();
    }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonPropertyOrder(2)]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModelType ModelType { get; set; } = ModelType.FileKeyClaim;

    [JsonPropertyName("ownerId")]
    [JsonPropertyOrder(10)]
    public Guid OwnerId { get; set; }

    /// <summary>True once the item that owns this key has been saved.</summary>
    [JsonPropertyName("committed")]
    [JsonPropertyOrder(11)]
    public bool Committed { get; set; }

    /// <summary>
    /// Allocator scope that may release this claim while it is uncommitted. A retry whose creating scope is gone
    /// replaces it with If-Match; a release deletes only when the stored value still matches.
    /// </summary>
    [JsonPropertyName("scopeId")]
    [JsonPropertyOrder(12)]
    public Guid ScopeId { get; set; }

    /// <summary>Cosmos per-item TTL. A day until the save keeps the claim, then none.</summary>
    [JsonPropertyName("ttl")]
    [JsonPropertyOrder(13)]
    public int? TimeToLiveSeconds { get; set; }

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(FileKeyClaim);
}
