using Reddit;

namespace RedditPodcastPoster.Reddit;

public interface IRedditClientFactory
{
    RedditClient Create();
}