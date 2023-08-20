using Reddit;

namespace RedditPodcastPoster.Common.Reddit;

public interface IRedditClientFactory
{
    RedditClient Create();
}