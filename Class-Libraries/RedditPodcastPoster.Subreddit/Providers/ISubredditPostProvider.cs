using Reddit.Controllers;

namespace RedditPodcastPoster.Subreddit.Providers;

public interface ISubredditPostProvider
{
    IEnumerable<Post> GetPosts();
}
