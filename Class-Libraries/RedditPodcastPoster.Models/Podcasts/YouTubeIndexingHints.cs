using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// What the hourly indexer last learned about a large YouTube playlist, so a later pass can find new uploads
/// without walking the whole playlist (see docs/catalogue-pagination.md, "YouTube indexing hints").
/// Only the indexer writes these. Curators never edit them; a "force full walk" clears them.
/// Every member is omitted when null, so documents saved before this type existed need no migration.
/// </summary>
public sealed record YouTubeIndexingHints
{
    /// <summary>
    /// The playlist these hints describe. When it is not the current <c>youTubePlaylistId</c> (a curator swapped
    /// the playlist), the hints are ignored and the next pass walks the new playlist in full.
    /// </summary>
    [JsonPropertyName("playlistId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PlaylistId { get; set; }

    /// <summary>UTC time of the last full playlist walk that read to the end.</summary>
    [JsonPropertyName("lastFullWalk")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? LastFullWalk { get; set; }

    /// <summary>
    /// The playlist's <c>pageInfo.totalResults</c> after the last full walk or uploads check. A new count that is not
    /// this plus the new members found sends the pass back to a full walk.
    /// </summary>
    [JsonPropertyName("playlistItemCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlaylistItemCount { get; set; }

    /// <summary>
    /// UTC publish time of the newest channel upload already checked against the playlist. Uploads at or before
    /// the later of this and <see cref="LastFullWalk"/> are not checked again.
    /// </summary>
    [JsonPropertyName("uploadsCheckedThrough")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTime? UploadsCheckedThrough { get; set; }

    /// <summary>
    /// The video id of the upload at <see cref="UploadsCheckedThrough"/>. Informational only (for reading the stored
    /// hints and the logs): nothing reads it, and the uploads check compares publish times alone.
    /// </summary>
    [JsonPropertyName("uploadsCheckedThroughVideoId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UploadsCheckedThroughVideoId { get; set; }

    /// <summary>A detached copy, so a pass can compare before and after.</summary>
    public YouTubeIndexingHints Copy() => this with { };
}
