using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeServiceFactory
{
    IYouTubeServiceWrapper Create();
    IYouTubeServiceWrapper Create(ApplicationUsage usage);
}