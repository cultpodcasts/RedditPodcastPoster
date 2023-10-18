using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeServiceFactory
{
    YouTubeService Create();
}