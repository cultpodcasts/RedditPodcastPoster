using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Subreddit.Repositories;

public interface ISubredditRepository
{
    IAsyncEnumerable<RedditPost> GetAll();
    Task Save(RedditPost post);
}
