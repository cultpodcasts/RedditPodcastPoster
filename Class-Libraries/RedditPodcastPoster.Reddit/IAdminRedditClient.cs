using Reddit;

namespace RedditPodcastPoster.Reddit;

public interface IAdminRedditClient
{
    RedditClient Client { get; init; }
}