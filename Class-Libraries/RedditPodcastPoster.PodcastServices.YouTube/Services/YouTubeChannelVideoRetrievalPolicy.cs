using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public class YouTubeChannelVideoRetrievalPolicy(IOptions<YouTubeChannelOptions> options)
    : IYouTubeChannelVideoRetrievalPolicy
{
    public bool ShouldUseUploadsPlaylist(Podcast podcast) =>
        GetUploadsPlaylistReason(podcast) != null;

    public string? GetUploadsPlaylistReason(Podcast podcast)
    {
        if (podcast.HasYouTubeChannelSearchForbidden())
        {
            return "youTubeChannelSearchForbidden";
        }

        if (options.Value.PreferUploadsPlaylist)
        {
            return "PreferUploadsPlaylist";
        }

        return null;
    }
}
