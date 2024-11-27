using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube.Factories;

public interface IYouTubeVideoServiceFactory
{
    YouTubeVideoService Create(ApplicationUsage applicationUsage);
}