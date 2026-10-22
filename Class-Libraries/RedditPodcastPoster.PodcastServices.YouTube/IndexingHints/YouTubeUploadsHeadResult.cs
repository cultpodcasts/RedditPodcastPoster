using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;

/// <summary>
/// The outcome of an uploads-head check. When <see cref="FallbackReason"/> is set the pass must walk the playlist in
/// full and the hints stay as they were.
/// </summary>
/// <param name="Members">New (not stored) uploads that are in the playlist, ready for the build step.</param>
/// <param name="TotalResults">The playlist's current <c>pageInfo.totalResults</c>.</param>
/// <param name="CheckedThrough">Window date of the newest upload checked, or null when no upload was newer.</param>
/// <param name="CheckedThroughVideoId">The video id at <see cref="CheckedThrough"/>.</param>
/// <param name="UploadsChecked">Uploads newer than the watermark that had their membership checked.</param>
/// <param name="FallbackReason">Why a full walk is needed instead, or null on success.</param>
public sealed record YouTubeUploadsHeadResult(
    IList<PlaylistItem> Members,
    int? TotalResults,
    DateTime? CheckedThrough,
    string? CheckedThroughVideoId,
    int UploadsChecked,
    string? FallbackReason)
{
    public bool Succeeded => FallbackReason == null;

    public static YouTubeUploadsHeadResult Fallback(string reason, int uploadsChecked = 0) =>
        new([], null, null, null, uploadsChecked, reason);
}
