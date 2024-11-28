using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.Bluesky.YouTube;

public class BlueskyYouTubeServiceFactory(IYouTubeServiceFactory youTubeServiceFactory) : IBlueskyYouTubeServiceFactory
{
    public IBlueskyYouTubeServiceWrapper Create()
    {
        return new BlueskyYouTubeServiceWrapper(youTubeServiceFactory.Create(ApplicationUsage.Bluesky));
    }
}