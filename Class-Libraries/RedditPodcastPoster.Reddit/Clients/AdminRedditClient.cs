using Reddit;

namespace RedditPodcastPoster.Reddit.Clients;

public class AdminRedditClient(RedditClient redditClient) : IAdminRedditClient
{
    public RedditClient Client { get; init; } = redditClient;
}