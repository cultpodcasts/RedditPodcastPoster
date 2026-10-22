using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface ICachedTolerantYouTubePlaylistService
{
    /// <param name="expensivePlaylist">
    /// Ignored: every playlist walk pages at <see cref="YouTubePlaylistService.PlaylistPageSize"/>.
    /// Kept for call-site compatibility; removal is tracked in #1104.
    /// </param>
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null);
}
