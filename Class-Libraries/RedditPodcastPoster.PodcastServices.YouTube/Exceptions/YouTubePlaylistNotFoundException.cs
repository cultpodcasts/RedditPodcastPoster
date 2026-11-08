namespace RedditPodcastPoster.PodcastServices.YouTube.Exceptions;

/// <summary>
/// playlists.list returned no item: the playlist does not exist or is private. Derives from
/// <see cref="InvalidOperationException"/> so callers that already catch that keep working.
/// </summary>
public class YouTubePlaylistNotFoundException(string playlistId)
    : InvalidOperationException($"Error obtaining playlist-snippet for playlist-id '{playlistId}'. No result.")
{
    public string PlaylistId { get; } = playlistId;
}
