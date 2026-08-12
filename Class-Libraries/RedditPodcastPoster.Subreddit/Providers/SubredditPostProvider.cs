using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Subreddit.Providers;

public class SubredditPostProvider(ILogger<SubredditPostProvider> logger) : ISubredditPostProvider
{
    public IEnumerable<RedditPost> GetPosts()
    {
        logger.LogInformation(
            "Reddit.NET subreddit post archive is retired; returning no posts.");
        return [];
    }
}
