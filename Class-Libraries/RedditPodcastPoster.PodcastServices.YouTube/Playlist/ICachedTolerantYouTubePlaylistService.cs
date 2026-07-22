using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface ICachedTolerantYouTubePlaylistService
{
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        bool expensivePlaylist = false);
}
