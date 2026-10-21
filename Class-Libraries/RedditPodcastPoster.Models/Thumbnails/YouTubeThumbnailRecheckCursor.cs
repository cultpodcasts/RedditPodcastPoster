using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Cosmos;

namespace RedditPodcastPoster.Models.Thumbnails;

/// <summary>
/// Cosmos LookUps singleton recording where the last YouTube thumbnail re-check run stopped, so the next run
/// resumes strictly after it (ordered by release time, then episode id) and every eligible episode gets an
/// equal turn. All fields are nullable: a missing document or null fields mean "start from the beginning".
/// </summary>
[CosmosSelector(ModelType.YouTubeThumbnailRecheckCursor)]
public sealed class YouTubeThumbnailRecheckCursor : CosmosSelector
{
    public static readonly Guid _Id = Guid.Parse("5e0b8a43-7c1d-4f2e-9a6b-3d8c1f0e7b29");

    public YouTubeThumbnailRecheckCursor()
    {
        Id = _Id;
        ModelType = ModelType.YouTubeThumbnailRecheckCursor;
    }

    /// <summary>Release time (UTC) of the last episode probed by the previous run.</summary>
    [JsonPropertyName("lastProbedReleaseUtc")]
    [JsonPropertyOrder(10)]
    public DateTime? LastProbedReleaseUtc { get; set; }

    /// <summary>Id of the last episode probed by the previous run (tie-break for equal release times).</summary>
    [JsonPropertyName("lastProbedEpisodeId")]
    [JsonPropertyOrder(11)]
    public Guid? LastProbedEpisodeId { get; set; }

    [JsonPropertyName("updatedUtc")]
    [JsonPropertyOrder(12)]
    public DateTime? UpdatedUtc { get; set; }

    public override string FileKey => nameof(YouTubeThumbnailRecheckCursor);
}
