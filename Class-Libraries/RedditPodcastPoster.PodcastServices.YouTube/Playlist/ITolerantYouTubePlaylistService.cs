using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface ITolerantYouTubePlaylistService
{
    /// <param name="expensivePlaylist">
    /// Ignored: every playlist walk pages at <see cref="YouTubePlaylistService.PlaylistPageSize"/>.
    /// Kept for call-site compatibility; removal is tracked in #1104.
    /// </param>
    public Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
        bool expensivePlaylist = false,
        PlaylistOrder? playlistOrder = null);

    /// <summary>
    /// <see cref="IYouTubePlaylistService.GetPlaylistHead"/> with api-key rotation on quota. When every key is spent the
    /// response has <see cref="YouTubePlaylistFetchFailure.QuotaExhausted"/>.
    /// </summary>
    public Task<GetPlaylistHeadResponse> GetPlaylistHead(
        YouTubePlaylistId playlistId,
        int maxResults,
        IndexingContext indexingContext);

    public Task<GetPlaylistInfoResponse> GetPlaylistInfo(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext);
}
