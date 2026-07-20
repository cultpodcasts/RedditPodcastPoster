using Reddit;

namespace RedditPodcastPoster.Reddit.Factories;

public interface IRedditClientFactory
{
    RedditClient Create();
}