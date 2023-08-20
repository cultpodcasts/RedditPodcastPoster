using Google.Apis.YouTube.v3;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public interface IYouTubeServiceFactory
{
    Task<YouTubeService> Create();
}