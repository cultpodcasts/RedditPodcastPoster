using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Films;

/// <summary>
/// Cosmos LookUps singleton for the FilmSearchPusher console: the highest film Cosmos <c>_ts</c> whose search
/// document was pushed by the last successful run. The next run reads films with <c>_ts &gt;=</c> this value
/// (the boundary second is re-pushed, because a film saved later in that same second would otherwise be missed).
/// A missing document or a null <see cref="HighWaterMark"/> means "push every film".
/// </summary>
[CosmosSelector(ModelType.FilmSearchPushWatermark)]
public sealed class FilmSearchPushWatermark : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("8c3f6d21-4b7a-4e0c-9f15-2a6e9d4b7c03");

    public FilmSearchPushWatermark()
    {
        Id = _Id;
        ModelType = ModelType.FilmSearchPushWatermark;
    }

    /// <summary>Highest film <c>_ts</c> (Unix seconds) pushed by the last successful run.</summary>
    [JsonPropertyName("highWaterMark")]
    [JsonPropertyOrder(10)]
    public long? HighWaterMark { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(11)]
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>Documents pushed (merge-or-upload) by the run that saved this watermark.</summary>
    [JsonPropertyName("lastRunPushed")]
    [JsonPropertyOrder(12)]
    public int? LastRunPushed { get; set; }

    /// <summary>Documents deleted (removed or missing films) by the run that saved this watermark.</summary>
    [JsonPropertyName("lastRunDeleted")]
    [JsonPropertyOrder(13)]
    public int? LastRunDeleted { get; set; }

    public override string FileKey => nameof(FilmSearchPushWatermark);
}
