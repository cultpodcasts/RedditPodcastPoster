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

    // Same rule as the appliers: whitespace clears, values are trimmed.
    private static string Normalise(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
