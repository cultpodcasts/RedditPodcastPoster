using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>
/// Response from GetPlaylistVideoSnippets, which may be null if the YouTube playlist API call failed (e.g. playlist gone).
/// </summary>
/// <param name="Result">The list of playlist items, or null if the API call failed.</param>
/// <param name="IsExpensiveQuery">Indicates if the query was expensive (oldest-first), null if inconclusive.</param>
/// <param name="Failure">The failure details, or null if the API call succeeded.</param>
public record GetPlaylistVideoSnippetsResponse(
    IList<PlaylistItem>? Result,
    bool? IsExpensiveQuery = null,
    YouTubePlaylistFetchFailure? Failure = null);
