using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface IYouTubePlaylistService
{
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        PlaylistOrder? playlistOrder = null);

    /// <summary>
    /// One playlistItems.list call (1 quota unit) for the first <paramref name="maxResults"/> items (at most 50) and
    /// <c>pageInfo.totalResults</c>. Used by the YouTube indexing hints: maxResults 1 reads a playlist's size, 50 reads
    /// the newest uploads of a channel's uploads playlist. Throws <c>YouTubeQuotaException</c> on quota, like the walk.
    /// </summary>
    Task<GetPlaylistHeadResponse> GetPlaylistHead(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        int maxResults,
        IndexingContext indexingContext);

    Task<GetPlaylistInfoResponse> GetPlaylistInfo(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext
    );
}
