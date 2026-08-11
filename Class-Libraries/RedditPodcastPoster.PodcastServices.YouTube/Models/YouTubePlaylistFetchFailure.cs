namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

/// <summary>
/// Why a playlistItems.list call returned no result and may have flipped SkipYouTubeUrlResolving.
/// </summary>
public enum YouTubePlaylistFetchFailure
{
    /// <summary>HTTP 404 — playlist deleted, private, or id wrong; update YouTubePlaylistId.</summary>
    NotFound,

    /// <summary>Other Google API / unexpected error while fetching playlist items.</summary>
    ApiError
}
