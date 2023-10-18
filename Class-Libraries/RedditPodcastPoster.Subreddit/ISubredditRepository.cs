using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subreddit;

public interface ISubredditRepository
{
    Task<IEnumerable<RedditPost>> GetAll();
    Task Save(RedditPost post);
}