using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public interface IYouTubeApiKeyStrategy
{
    Application GetApplication(ApplicationUsage usage);
}