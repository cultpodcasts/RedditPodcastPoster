using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeChannelVideoRetrievalPolicy
{
    bool ShouldUseUploadsPlaylist(Podcast podcast);

    string? GetUploadsPlaylistReason(Podcast podcast);
}
