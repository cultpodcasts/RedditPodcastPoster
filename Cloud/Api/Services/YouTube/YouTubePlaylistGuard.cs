using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

public class YouTubePlaylistGuard(IYouTubePlaylistValidator validator) : IYouTubePlaylistGuard
{
    public async Task<YouTubePlaylistValidation?> Check(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        CancellationToken cancellationToken)
    {
        if (requestedPlaylistId == null)
        {
            return null;
        }

        var playlistId = Normalise(requestedPlaylistId);
        if (playlistId.Length == 0 || string.Equals(playlistId, Normalise(currentPlaylistId), StringComparison.Ordinal))
        {
            // Clearing the playlist, or re-sending the stored one, needs no check.
            return null;
        }

        var validation = await validator.Validate(playlistId, cancellationToken);
        return validation.IsAllowed ? null : validation;
    }

    public async Task<YouTubePlaylistValidation?> CheckChannelAgainstPlaylist(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        string? currentChannelId,
        string? requestedChannelId,
        CancellationToken cancellationToken)
    {
        if (requestedChannelId == null)
        {
            return null;
        }

        var channelId = Normalise(requestedChannelId);
        if (channelId.Length == 0 ||
            string.Equals(channelId, Normalise(currentChannelId), StringComparison.Ordinal))
        {
            // Clearing the channel, or re-sending the stored one, needs no check.
            return null;
        }

        // Post-request playlist: request clears → skip; request sets new id → that id; else stored.
        var playlistId = requestedPlaylistId != null
            ? Normalise(requestedPlaylistId)
            : Normalise(currentPlaylistId);
        if (playlistId.Length == 0)
        {
            // No playlist remains: channel is the YouTube source on its own.
            return null;
        }

        var validation = await validator.Validate(playlistId, cancellationToken);
        if (!validation.IsAllowed)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(validation.OwnerChannelId))
        {
            // Valid without an owner fail-opens the ownership gate — refuse as retryable lookup failure.
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

    // Same rule as the appliers: whitespace clears, values are trimmed.
    private static string Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
