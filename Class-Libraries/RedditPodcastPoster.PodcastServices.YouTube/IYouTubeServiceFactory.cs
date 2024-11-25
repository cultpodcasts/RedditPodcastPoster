using Google.Apis.YouTube.v3;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeServiceFactory
{
    YouTubeService Create(ApplicationUsage usage);
}