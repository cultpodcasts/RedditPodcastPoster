using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Factories;

namespace RedditPodcastPoster.Bluesky.YouTube;

public class BlueskyYouTubeServiceFactory(IYouTubeServiceFactory youTubeServiceFactory) : IBlueskyYouTubeServiceFactory
{
    public IBlueskyYouTubeServiceWrapper Create()
    {
        return new BlueskyYouTubeServiceWrapper(youTubeServiceFactory.Create(ApplicationUsage.Bluesky));
    }
}