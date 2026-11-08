using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

/// <summary>
/// Save-time helper: a playlist Check that returned Valid already paid 1 quota unit for OwnerChannelId.
/// Persist that owner onto a blank parent channel so discovery / URL routing can match by channel id.
/// Never overwrites a non-blank intentional channel (playlist-only indexing still uses the playlist alone).
/// Also records <c>youTubePlaylistOwnerChannelId</c> so the indexer can skip uploads-head when the parent
/// channel does not own the playlist (#1183).
/// </summary>
public static class YouTubePlaylistOwnerChannel
{
    /// <summary>
    /// Returns the playlist owner to store when <paramref name="playlistCheck"/> is Valid with a non-blank owner
    /// and <paramref name="currentChannelId"/> is blank; otherwise null (leave the channel as-is).
    /// </summary>
    public static string? DerivedOwnerIfChannelBlank(
        YouTubePlaylistValidation? playlistCheck,
        string? currentChannelId)
    {
        if (!string.IsNullOrWhiteSpace(currentChannelId))
        {
            return null;
        }

        if (playlistCheck?.Status != YouTubePlaylistValidationStatus.Valid ||
            string.IsNullOrWhiteSpace(playlistCheck.OwnerChannelId))
        {
            return null;
        }

        return playlistCheck.OwnerChannelId.Trim();
    }

    /// <summary>
    /// Owner channel id from a Valid playlist Check (trimmed), or null when Check is missing / not Valid /
    /// has no owner. Used to persist <c>youTubePlaylistOwnerChannelId</c> for the uploads-head skip (#1183).
    /// </summary>
    public static string? OwnerFromValidCheck(YouTubePlaylistValidation? playlistCheck)
    {
        if (playlistCheck?.Status != YouTubePlaylistValidationStatus.Valid ||
            string.IsNullOrWhiteSpace(playlistCheck.OwnerChannelId))
        {
            return null;
        }

        return playlistCheck.OwnerChannelId.Trim();
    }

    /// <summary>
    /// Persist <c>youTubePlaylistOwnerChannelId</c> from a Valid playlist Check: clear when the effective playlist
    /// is blank; set from Check when Valid; otherwise leave the stored owner unchanged (e.g. unchanged playlist
    /// that already has an owner, or soft fill that did not return Valid).
    /// </summary>
    public static void PersistFromCheck(
        string? effectivePlaylistId,
        YouTubePlaylistValidation? playlistCheck,
        Action clearOwner,
        Action<string> setOwner)
    {
        if (string.IsNullOrWhiteSpace(effectivePlaylistId))
        {
            clearOwner();
            return;
        }

        if (OwnerFromValidCheck(playlistCheck) is { } playlistOwner)
        {
            setOwner(playlistOwner);
        }
    }
}
