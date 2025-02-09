using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface IYouTubePlaylistService
{
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
        IYouTubeServiceWrapper youTubeServiceWrapper,
        YouTubePlaylistId playlistId,
        IndexingContext indexingContext,
        bool withContentDetails = false);
}