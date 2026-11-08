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
    /// Null when no playlist check ran — the save may go ahead and no owner was fetched. Runs a
    /// <see cref="IYouTubePlaylistValidator.Validate"/> when:
    /// <list type="bullet">
    /// <item>the request sets a new non-blank playlist id (refusal blocks the save), or</item>
    /// <item>a non-blank playlist remains after the request and
    /// <paramref name="currentPlaylistOwnerChannelId"/> is blank (fill-when-owner-blank for #1183; only a Valid
    /// result is returned — a failed lookup does not block an unrelated edit).</item>
    /// </list>
    /// Clearing the playlist, or leaving an unchanged playlist that already has an owner, costs no quota.
    /// A <see cref="YouTubePlaylistValidationStatus.Valid"/> result carries <c>OwnerChannelId</c> for callers to
    /// persist onto a blank parent channel and/or <c>youTubePlaylistOwnerChannelId</c>.
    /// </summary>
    Task<YouTubePlaylistValidation?> Check(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        string? currentPlaylistOwnerChannelId,
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

    /// <summary>
    /// Null when a stored channel/playlist pair may be copied onto another parent (kind transfer). Otherwise the
    /// refused validation. Only checks when both ids are non-blank; see
    /// <see cref="YouTubePlaylistChannelOwnership"/>.
    /// </summary>
    Task<YouTubePlaylistValidation?> CheckChannelOwnsPlaylist(
        string? channelId,
        string? playlistId,
        CancellationToken cancellationToken);
}
