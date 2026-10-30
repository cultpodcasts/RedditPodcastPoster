namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Checks that a YouTube playlist is owned by the given YouTube channel. The indexer keeps only playlist videos uploaded
/// by the parent's channel, so a playlist owned by another channel silently indexes nothing (e.g. Fame Under Fire,
/// Oct 2026). Write paths use this to refuse such a pair.
/// </summary>
public interface IYouTubePlaylistOwnershipValidator
{
    /// <summary>
    /// <see cref="YouTubePlaylistOwnershipStatus.NotApplicable"/> when either id is blank (no YouTube call is made).
    /// Never throws for a YouTube failure: that is <see cref="YouTubePlaylistOwnershipStatus.LookupFailed"/>.
    /// </summary>
    Task<YouTubePlaylistOwnership> Validate(string? youTubeChannelId, string? youTubePlaylistId,
        CancellationToken cancellationToken = default);
}
