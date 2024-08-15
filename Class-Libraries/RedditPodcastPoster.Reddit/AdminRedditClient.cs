using Reddit;

namespace RedditPodcastPoster.Reddit;

public class AdminRedditClient(RedditClient redditClient) : IAdminRedditClient
{
    public RedditClient Client { get; init; } = redditClient;
}