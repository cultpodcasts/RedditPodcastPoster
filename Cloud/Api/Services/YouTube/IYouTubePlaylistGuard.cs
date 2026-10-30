using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

/// <summary>
/// Edit-time guard: a parent (podcast, news organisation, TV show) may not be saved with a malformed, missing or
/// private YouTube playlist id. The playlist's owner channel is not compared with the parent's channel id: with a
/// playlist, the playlist alone is the parent's YouTube source. See <see cref="IYouTubePlaylistValidator"/>.
/// </summary>
public interface IYouTubePlaylistGuard
{
    /// <summary>
    /// Null when the save may go ahead. Otherwise the refused validation (invalid id, playlist not found, or a
    /// retryable lookup failure). Only checks when the request sets the playlist id to a new non-blank value (null =
    /// the request leaves it unchanged), so an unrelated edit of an existing parent is never blocked and costs no quota.
    /// </summary>
    Task<YouTubePlaylistValidation?> Check(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        CancellationToken cancellationToken);
}
