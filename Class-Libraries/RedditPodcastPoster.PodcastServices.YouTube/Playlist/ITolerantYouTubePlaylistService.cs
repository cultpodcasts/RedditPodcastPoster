using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.ChannelSnippets;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Playlist;

public interface ITolerantYouTubePlaylistService
{
    public Task<GetPlaylistVideoSnippetsResponse> GetPlaylistVideoSnippets(
    YouTubePlaylistId playlistId,
    IndexingContext indexingContext);
}
