using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

/// <summary>
/// Edit-time guard: a parent (podcast, news organisation, TV show) may not be saved with a YouTube playlist owned by a
/// channel other than its YouTube channel id. See <see cref="IYouTubePlaylistOwnershipValidator"/>.
/// </summary>
public interface IYouTubePlaylistOwnershipGuard
{
    /// <summary>
    /// Null when the save may go ahead. Otherwise the refused ownership result (mismatch, playlist not found, or a
    /// retryable lookup failure). Only checks when the request changes the playlist id or the channel id (null = the
    /// request leaves it unchanged), so an unrelated edit of an existing parent is never blocked and costs no quota.
    /// </summary>
    Task<YouTubePlaylistOwnership?> Check(
        string? currentChannelId,
        string? currentPlaylistId,
        string? requestedChannelId,
        string? requestedPlaylistId,
        CancellationToken cancellationToken);
}
