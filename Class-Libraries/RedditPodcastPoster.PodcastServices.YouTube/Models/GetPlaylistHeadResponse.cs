using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>
/// One playlistItems.list page from the head of a playlist (1 quota unit): its items and <c>pageInfo.totalResults</c>.
/// </summary>
/// <param name="Items">The page's items in playlist order, or null when the call failed.</param>
/// <param name="TotalResults">The playlist's <c>pageInfo.totalResults</c>, when YouTube reported one.</param>
/// <param name="Failure">Why the call failed, or null on success.</param>
public record GetPlaylistHeadResponse(
    IList<PlaylistItem>? Items,
    int? TotalResults = null,
    YouTubePlaylistFetchFailure? Failure = null);
