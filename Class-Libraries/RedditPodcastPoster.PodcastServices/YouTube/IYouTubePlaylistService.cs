using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Spotify;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubePlaylistService
{
    Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(YouTubePlaylistId playlistId,
        IndexingContext indexingContext);
}