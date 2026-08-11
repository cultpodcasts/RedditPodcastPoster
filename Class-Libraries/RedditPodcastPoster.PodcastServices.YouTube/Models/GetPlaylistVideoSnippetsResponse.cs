using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistVideoSnippetsResponse(
    IList<PlaylistItem>? Result,
    /// <summary>
    /// Playlist-order probe: true = oldest-first (expensive), false = newest-first, null = inconclusive.
    /// </summary>
    bool? IsExpensiveQuery = null,
    /// <summary>
    /// Set when Result is null because the YouTube playlist API call failed (e.g. playlist gone).
    /// </summary>
    YouTubePlaylistFetchFailure? Failure = null);
