using RedditPodcastPoster.Reddit.Clients;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IAdminRedditClientFactory
{
    IAdminRedditClient Create();
}