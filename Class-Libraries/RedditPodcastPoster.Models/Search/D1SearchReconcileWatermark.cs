using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Search;

/// <summary>
/// Cosmos LookUps singleton for <c>D1SearchReconcile</c>: the highest Cosmos <c>_ts</c> considered by the last
/// successful write. The next incremental run reads playables with <c>_ts &gt;=</c> this value, plus every episode
/// of a podcast whose <c>_ts</c> is in that window (a podcast rename or subject change must re-emit its episodes).
/// The boundary second is re-read, because a document saved later in that same second would otherwise be missed
/// (same rule as <see cref="Films.FilmSearchPushWatermark"/>). A missing document or a null
/// <see cref="HighWaterMark"/> means "scan the whole catalogue". The watermark advances only after a successful
/// <c>--write</c> run.
/// </summary>
[CosmosSelector(ModelType.D1SearchReconcileWatermark)]
public sealed class D1SearchReconcileWatermark : CosmosSelector, IFileKeyed
{
    public static readonly Guid _Id = Guid.Parse("c4e91b70-2f6a-4d8e-9b13-7a5c0e2d84f1");

    public D1SearchReconcileWatermark()
    {
        Id = _Id;
        ModelType = ModelType.D1SearchReconcileWatermark;
    }

    /// <summary>Highest Cosmos <c>_ts</c> (Unix seconds) considered by the last successful write.</summary>
    [JsonPropertyName("highWaterMark")]
    [JsonPropertyOrder(10)]
    public long? HighWaterMark { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(11)]
    public DateTime? UpdatedUtc { get; set; }

    [JsonPropertyName("fileKey")]
    [JsonPropertyOrder(1000)]
    public string FileKey => nameof(D1SearchReconcileWatermark);
}
