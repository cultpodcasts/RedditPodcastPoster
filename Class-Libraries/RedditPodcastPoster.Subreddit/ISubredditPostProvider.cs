using Reddit.Controllers;

namespace RedditPodcastPoster.Subreddit;

public interface ISubredditPostProvider
{
    IEnumerable<Post> GetPosts();
}