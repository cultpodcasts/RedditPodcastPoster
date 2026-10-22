using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public record GetPlaylistEpisodesResponse(
    IList<EpisodeModel>? Results,
    /// <summary>
    /// Playlist-order probe: true = oldest-first (expensive), false = newest-first, null = inconclusive.
    /// </summary>
    bool? IsExpensiveQuery = null,
    /// <summary>
    /// Set when Results is null because the YouTube playlist API call failed (e.g. playlist gone).
    /// </summary>
    YouTubePlaylistFetchFailure? Failure = null,
    /// <summary>
    /// In-window playlist items dropped because the podcast already stores them (known-id skip). In-window items found
    /// = <c>Results.Count + KnownSkipped</c>.
    /// </summary>
    int KnownSkipped = 0);
