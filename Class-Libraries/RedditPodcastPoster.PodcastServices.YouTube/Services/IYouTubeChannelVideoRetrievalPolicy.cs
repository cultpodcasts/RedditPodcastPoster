using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeChannelVideoRetrievalPolicy
{
    bool ShouldUseUploadsPlaylist(Podcast podcast);

    string? GetUploadsPlaylistReason(Podcast podcast);
}
