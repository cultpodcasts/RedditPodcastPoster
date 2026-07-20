using Reddit;

namespace RedditPodcastPoster.Reddit.Clients;

public interface IAdminRedditClient
{
    RedditClient Client { get; init; }
}