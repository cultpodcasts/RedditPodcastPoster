using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public interface IYouTubeServiceFactory
{
    IYouTubeServiceWrapper Create();
    IYouTubeServiceWrapper Create(ApplicationUsage usage);
}