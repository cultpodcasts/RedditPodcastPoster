using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subreddit;

public interface ISubredditRepository
{
    IAsyncEnumerable<RedditPost> GetAll();
    Task Save(RedditPost post);
}