namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

/// <summary>
/// Checks that a YouTube playlist id is well formed and that the playlist exists (and is public). When a parent has a
/// playlist id the playlist is its only YouTube source (every video in it is indexed, whichever channel uploaded it),
/// so a bad id silently indexes nothing. Write paths use this to refuse such an id.
/// </summary>
public interface IYouTubePlaylistValidator
{
    /// <summary>
    /// <see cref="YouTubePlaylistValidationStatus.NotApplicable"/> when the id is blank (no YouTube call is made).
    /// Never throws for a YouTube failure: that is <see cref="YouTubePlaylistValidationStatus.LookupFailed"/>.
    /// </summary>
    Task<YouTubePlaylistValidation> Validate(string? youTubePlaylistId, CancellationToken cancellationToken = default);
}
