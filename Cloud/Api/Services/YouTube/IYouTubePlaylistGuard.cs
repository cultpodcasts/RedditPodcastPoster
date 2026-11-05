using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

/// <summary>
/// Edit-time guard for parent YouTube playlist / channel ids.
/// <list type="bullet">
/// <item>Playlist edits: refuse a malformed, missing or private playlist id. The playlist's owner channel is not
/// compared with the parent's channel id — with a playlist, the playlist alone is the parent's YouTube source.</item>
/// <item>Channel edits (podcast): when a playlist remains after the request (post-apply), the requested channel must
/// own that playlist, so discovery / URL routing identity matches the playlist owner. Clearing the channel, leaving
/// it unchanged, clearing the playlist in the same request, or setting a channel with no effective playlist costs no
/// quota.</item>
/// </list>
/// See <see cref="IYouTubePlaylistValidator"/>.
/// </summary>
public interface IYouTubePlaylistGuard
{
    /// <summary>
    /// Null when no playlist check ran (request leaves the playlist unchanged, clears it, or re-sends the stored id) —
    /// the save may go ahead and no owner was fetched. When the request sets a new non-blank playlist id: a
    /// <see cref="YouTubePlaylistValidationStatus.Valid"/> result (with <c>OwnerChannelId</c> when YouTube returned one)
    /// means the save may go ahead and callers may persist that owner onto a blank parent channel; otherwise the
    /// refused validation (invalid id, playlist not found, or a retryable lookup failure).
    /// </summary>
    Task<YouTubePlaylistValidation?> Check(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Null when the channel change may go ahead. Otherwise the refused validation (channel does not own the
    /// post-request playlist, playlist missing/malformed, blank owner on an otherwise-valid lookup, or a retryable
    /// lookup failure). Only checks when the request sets the channel id to a new non-blank value and a playlist
    /// remains after applying the request's playlist field (null = leave stored; whitespace/"" = clear → skip; new id
    /// → check that id).
    /// </summary>
    Task<YouTubePlaylistValidation?> CheckChannelAgainstPlaylist(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        string? currentChannelId,
        string? requestedChannelId,
        CancellationToken cancellationToken);
}
