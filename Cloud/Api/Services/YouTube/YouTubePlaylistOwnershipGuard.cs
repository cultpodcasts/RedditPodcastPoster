using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace Api.Services.YouTube;

public class YouTubePlaylistOwnershipGuard(IYouTubePlaylistOwnershipValidator validator) : IYouTubePlaylistOwnershipGuard
{
    public async Task<YouTubePlaylistOwnership?> Check(
        string? currentChannelId,
        string? currentPlaylistId,
        string? requestedChannelId,
        string? requestedPlaylistId,
        CancellationToken cancellationToken)
    {
        var playlistChanged = requestedPlaylistId != null &&
                              !string.Equals(Normalise(requestedPlaylistId), Normalise(currentPlaylistId),
                                  StringComparison.Ordinal);
        var channelChanged = requestedChannelId != null &&
                             !string.Equals(Normalise(requestedChannelId), Normalise(currentChannelId),
                                 StringComparison.Ordinal);
        if (!playlistChanged && !channelChanged)
        {
            return null;
        }

        var channelId = requestedChannelId != null ? Normalise(requestedChannelId) : Normalise(currentChannelId);
        var playlistId = requestedPlaylistId != null ? Normalise(requestedPlaylistId) : Normalise(currentPlaylistId);
        var ownership = await validator.Validate(channelId, playlistId, cancellationToken);
        return ownership.IsAllowed ? null : ownership;
    }

    // Same rule as the appliers: whitespace clears, values are trimmed.
    private static string Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
