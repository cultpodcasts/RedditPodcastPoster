using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.ChannelVideos;

public interface IYouTubeChannelVideosService
{
    /// <param name="expensivePlaylist">
    /// Ignored: every playlist walk pages at <see cref="Playlist.YouTubePlaylistService.PlaylistPageSize"/>.
    /// Kept for call-site compatibility; removal is tracked in #1104.
    /// </param>
    Task<GetChannelVideosResponse> GetChannelVideos(
        YouTubeChannelId channelId,
        IndexingContext indexingContext,
        bool expensivePlaylist = false);
}
