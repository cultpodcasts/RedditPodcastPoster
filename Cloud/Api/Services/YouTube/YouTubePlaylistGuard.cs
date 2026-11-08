using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

public class YouTubePlaylistGuard(IYouTubePlaylistValidator validator) : IYouTubePlaylistGuard
{
    public async Task<YouTubePlaylistValidation?> Check(
        string? currentPlaylistId,
        string? requestedPlaylistId,
        string? currentPlaylistOwnerChannelId,
        CancellationToken cancellationToken)
    {
        // Post-request playlist: null request → leave stored; whitespace/"" → clear; else requested id.
        var effectivePlaylistId = requestedPlaylistId != null
            ? Normalise(requestedPlaylistId)
            : Normalise(currentPlaylistId);

        if (effectivePlaylistId.Length == 0)
        {
            // Clearing the playlist needs no check.
            return null;
        }

        var currentNormalised = Normalise(currentPlaylistId);
        var playlistIdChanged = !string.Equals(effectivePlaylistId, currentNormalised, StringComparison.Ordinal);

        if (playlistIdChanged)
        {
            // New non-blank playlist id: Valid (with OwnerChannelId) or a refusal that blocks the save.
            return await validator.Validate(effectivePlaylistId, cancellationToken);
        }

        // Unchanged non-blank playlist: Validate once when owner is missing so legacy foreign-playlist parents
        // can persist youTubePlaylistOwnerChannelId (#1183). Soft — a failed lookup does not block the save.
        if (string.IsNullOrWhiteSpace(currentPlaylistOwnerChannelId))
        {
            var fill = await validator.Validate(effectivePlaylistId, cancellationToken);
            return fill.Status == YouTubePlaylistValidationStatus.Valid ? fill : null;
        }

        // Re-sending / omitting a playlist that already has an owner needs no check.
        return null;
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

        return await YouTubePlaylistChannelOwnership.Check(validator, channelId, playlistId, cancellationToken);
    }

    public Task<YouTubePlaylistValidation?> CheckChannelOwnsPlaylist(
        string? channelId,
        string? playlistId,
        CancellationToken cancellationToken) =>
        YouTubePlaylistChannelOwnership.Check(validator, channelId, playlistId, cancellationToken);

    // Same rule as the appliers: whitespace clears, values are trimmed.
    private static string Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
