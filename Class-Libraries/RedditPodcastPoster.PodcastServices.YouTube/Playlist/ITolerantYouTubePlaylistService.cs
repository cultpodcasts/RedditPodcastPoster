using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface ITolerantYouTubePlaylistService
{
    public Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false,
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
