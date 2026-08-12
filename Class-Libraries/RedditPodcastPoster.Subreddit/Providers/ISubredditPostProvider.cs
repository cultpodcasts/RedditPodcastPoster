using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Subreddit.Providers;

/// <summary>
/// Live subreddit post archive via Reddit.NET is retired.
/// </summary>
public interface ISubredditPostProvider
{
    IEnumerable<RedditPost> GetPosts();
}
