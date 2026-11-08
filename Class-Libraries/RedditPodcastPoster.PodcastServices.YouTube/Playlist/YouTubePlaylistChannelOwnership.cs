namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Copy-path rule for a stored <c>youTubeChannelId</c> / <c>youTubePlaylistId</c> pair: when both are set, the
/// channel must own the playlist (discovery / URL-routing identity). A channel-only or playlist-only parent is fine
/// (with a playlist, the playlist alone is the YouTube source). Used by kind transfer and CatalogueMigrate before they
/// copy the pair onto a TV / news parent; edit-time channel changes use
/// <c>IYouTubePlaylistGuard.CheckChannelAgainstPlaylist</c>, which applies the same ownership comparison.
/// </summary>
public static class YouTubePlaylistChannelOwnership
{
    /// <summary>
    /// Null when the pair may be copied. Otherwise the refused validation (invalid / not found / channel mismatch /
    /// retryable lookup failure, including a Valid lookup with a blank owner so ownership never fails open).
    /// </summary>
    public static async Task<YouTubePlaylistValidation?> Check(
        IYouTubePlaylistValidator validator,
        string? youTubeChannelId,
        string? youTubePlaylistId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var channelId = Normalise(youTubeChannelId);
        var playlistId = Normalise(youTubePlaylistId);
        if (channelId.Length == 0 || playlistId.Length == 0)
        {
            return null;
        }

        var validation = await validator.Validate(playlistId, cancellationToken);
        if (!validation.IsAllowed)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(validation.OwnerChannelId))
        {
            return new YouTubePlaylistValidation(
                YouTubePlaylistValidationStatus.LookupFailed,
                playlistId);
        }

        if (string.Equals(validation.OwnerChannelId, channelId, StringComparison.Ordinal))
        {
            return null;
        }

        return new YouTubePlaylistValidation(
            YouTubePlaylistValidationStatus.ChannelMismatch,
            playlistId,
            validation.OwnerChannelId,
            validation.OwnerChannelTitle);
    }

    private static string Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
